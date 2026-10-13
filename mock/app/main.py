"""Mock WebUntis — the system's only backend (issue #3 § Architecture; PROJECT-BRIEF.md §0a.1).

Plain REST, not WebUntis's own protocol: there is no real WebUntis tenant reachable and no Node
sidecar translating for one, so `IWebUntisClient` (server/TimeForYou.WebUntis) will call this
service directly. Serves the read surface — auth, timetable, subjects, and QR-code generation for
the onboarding demo — plus the unauthenticated admin mutation endpoints (`/mock/cancel-lesson/{id}`,
`/mock/move-lesson/{id}`, `/mock/change-room/{id}`, `/mock/restore-lesson/{id}`, `/mock/reset`) that
drive change detection in tests and on stage. `/demo` is the button-driven page over those, with
`/mock/lessons` listing lessons and `/mock/nudge` proxying the worker's `POST /nudge` (the browser
cannot call the worker cross-origin). LAN is the boundary for all of it; do not add a token.

Several students are served (issue #129): the demo student, one per developer, and throwaway guests
created from `/demo` (`POST /mock/guests`, in memory only). `/qr-payload`, `/qr-code` and
`/mock/lessons` take `?student=<school:user>` and default to the first student.
"""

import io
import json
import os
import random
import re
import secrets
import urllib.error
import urllib.request
import uuid
from datetime import date, datetime, time, timedelta, timezone
from pathlib import Path

import qrcode
from fastapi import FastAPI, Header, HTTPException, Query
from fastapi.responses import FileResponse, JSONResponse, StreamingResponse

from app.fixtures import load_students, student_key
from app.schemas import (
    ChangeRoomRequest,
    GuestRequest,
    LessonOut,
    LoginRequest,
    LoginResponse,
    MoveLessonRequest,
    ProfileOut,
    QrPayload,
    StudentOut,
    SubjectOut,
)

# Eduvos is a South African institution; SAST has no DST, so a fixed UTC+2 offset is always correct.
SAST = timezone(timedelta(hours=2))

app = FastAPI(title="TimeForYou Mock WebUntis")

_students = load_students()
_sessions: dict[str, str] = {}  # session_id -> student key

# Occurrence id -> field overrides. This is the mutable state layer: fixtures on disk are the
# template and are never written to, so `/mock/reset` is just clearing this dict.
_overrides: dict[str, dict] = {}

_DEMO_PAGE = Path(__file__).parent / "static" / "demo.html"
_WORKER_NUDGE_URL = os.environ.get("WORKER_NUDGE_URL", "http://worker:8081/nudge")

_OCCURRENCE_ID_RE = re.compile(r"(\d+)-(\d{4}-\d{2}-\d{2})")


def _student_or_404(key: str | None) -> dict:
    """The student named by `?student=`, or the first (demo) student when none is given."""
    if key is None:
        return next(iter(_students.values()))
    student = _students.get(key)
    if student is None:
        raise HTTPException(status_code=404, detail="unknown student")
    return student


def _authenticate(authorization: str | None) -> dict:
    if authorization is None or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="missing bearer session")
    session_id = authorization.removeprefix("Bearer ")
    student = _students.get(_sessions.get(session_id, ""))
    if student is None:
        raise HTTPException(status_code=401, detail="unknown or expired session")
    return student


@app.post("/login", response_model=LoginResponse)
def login(body: LoginRequest) -> LoginResponse:
    key = student_key(body.school, body.user)
    student = _students.get(key)
    if student is None or student["server"] != body.server or student["key"] != body.key:
        raise HTTPException(status_code=401, detail="unknown WebUntis credentials")
    session_id = uuid.uuid4().hex
    _sessions[session_id] = key
    return LoginResponse(session_id=session_id)


def _week_monday(d: date) -> date:
    return d - timedelta(days=d.weekday())


def _occurrence_id(template_id: int, occurrence_date: date) -> str:
    return f"{template_id}-{occurrence_date.isoformat()}"


def _mode_for(student: dict, occurrence_date: date) -> str:
    """Weeks alternate online / in-person for the whole block, starting online in the block's first week."""
    block_start = date.fromisoformat(student["block_start"])
    weeks_in = (_week_monday(occurrence_date) - _week_monday(block_start)).days // 7
    return "online" if weeks_in % 2 == 0 else "in_person"


def _lesson_out(student: dict, lesson: dict, occurrence_date: date) -> LessonOut:
    """Builds one occurrence of a template lesson on a given date, with any admin override applied."""
    subject = next(s for s in student["subjects"] if s["id"] == lesson["subject_id"])
    occurrence_id = _occurrence_id(lesson["id"], occurrence_date)
    override = _overrides.get(occurrence_id, {})
    default_start = datetime.combine(occurrence_date, time.fromisoformat(lesson["start_time"]), tzinfo=SAST)
    default_end = datetime.combine(occurrence_date, time.fromisoformat(lesson["end_time"]), tzinfo=SAST)
    # Assigning a room by hand is the demo's "this lesson is now on campus", so it overrides the week's mode.
    mode = "in_person" if "room" in override else _mode_for(student, occurrence_date)
    return LessonOut(
        untis_lesson_id=occurrence_id,
        subject_code=subject["code"],
        subject_name=subject["name"],
        # Online lessons have no room.
        room=override.get("room", lesson["room"]) if mode == "in_person" else None,
        lecturer=lesson["lecturer"],
        group=lesson["group"],
        mode=mode,
        status=override.get("status", lesson["status"]),
        start_ts=override.get("start_ts", default_start),
        end_ts=override.get("end_ts", default_end),
    )


def _materialize(student: dict, start: date, end: date) -> list[LessonOut]:
    """Projects the fixture's weekly template onto every real calendar occurrence in [start, end].

    Lessons stay demoable regardless of when the mock is run — "this week" is always the current
    week rather than a fixed date that goes stale before the showcase.
    """
    lessons = []
    day = start
    while day <= end:
        for lesson in student["lessons"]:
            if lesson["weekday"] == day.weekday():
                lesson_out = _lesson_out(student, lesson, day)
                # An override can move a lesson's displayed start_ts off its template day, so
                # filter on the value actually returned, not the calendar day being scanned.
                if start <= lesson_out.start_ts.date() <= end:
                    lessons.append(lesson_out)
        day += timedelta(days=1)
    return lessons


def _find_occurrence(occurrence_id: str) -> tuple[dict, dict, date] | None:
    """Resolves an occurrence id back to its (student, template lesson, occurrence date)."""
    match = _OCCURRENCE_ID_RE.fullmatch(occurrence_id)
    if match is None:
        return None
    template_id = int(match.group(1))
    occurrence_date = date.fromisoformat(match.group(2))
    for student in _students.values():
        for lesson in student["lessons"]:
            if lesson["id"] == template_id and lesson["weekday"] == occurrence_date.weekday():
                return student, lesson, occurrence_date
    return None


def _require_occurrence(occurrence_id: str) -> tuple[dict, dict, date]:
    found = _find_occurrence(occurrence_id)
    if found is None:
        raise HTTPException(status_code=404, detail="unknown lesson occurrence")
    return found


@app.get("/timetable", response_model=list[LessonOut])
def get_timetable(
    authorization: str | None = Header(default=None),
    start: date | None = Query(default=None),
    end: date | None = Query(default=None),
) -> list[LessonOut]:
    student = _authenticate(authorization)
    range_start = start or _week_monday(datetime.now(SAST).date())
    range_end = end or (range_start + timedelta(days=6))
    return _materialize(student, range_start, range_end)

@app.get("/subjects", response_model=list[SubjectOut])
def get_subjects(authorization: str | None = Header(default=None)) -> list[SubjectOut]:
    student = _authenticate(authorization)
    return [SubjectOut(**subject) for subject in student["subjects"]]


@app.get("/profile", response_model=ProfileOut)
def get_profile(authorization: str | None = Header(default=None)) -> ProfileOut:
    """The signed-in student's display name and class, as WebUntis shows them (#113)."""
    student = _authenticate(authorization)
    return ProfileOut(display_name=student["display_name"], class_name=student.get("class_name"))


@app.get("/qr-payload", response_model=QrPayload)
def qr_payload(student: str | None = Query(default=None)) -> QrPayload:
    student = _student_or_404(student)
    return QrPayload(
        server=student["server"],
        school=student["school"],
        user=student["user"],
        key=student["key"],
    )


@app.get("/qr-code")
def qr_code(student: str | None = Query(default=None)) -> StreamingResponse:
    """A real, scannable QR image — the demo operator opens this in a browser and a phone scans
    it directly, the same shape the app expects from a genuine WebUntis SSO QR code."""
    payload = qr_payload(student)
    image = qrcode.make(json.dumps(payload.model_dump()))
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="image/png")


@app.post("/mock/cancel-lesson/{lesson_id}", response_model=LessonOut)
def cancel_lesson(lesson_id: str) -> LessonOut:
    student, lesson, occurrence_date = _require_occurrence(lesson_id)
    _overrides.setdefault(lesson_id, {})["status"] = "cancelled"
    return _lesson_out(student, lesson, occurrence_date)


@app.post("/mock/move-lesson/{lesson_id}", response_model=LessonOut)
def move_lesson(lesson_id: str, body: MoveLessonRequest) -> LessonOut:
    student, lesson, occurrence_date = _require_occurrence(lesson_id)
    override = _overrides.setdefault(lesson_id, {})
    override["start_ts"] = body.start_ts
    override["end_ts"] = body.end_ts
    return _lesson_out(student, lesson, occurrence_date)


@app.post("/mock/change-room/{lesson_id}", response_model=LessonOut)
def change_room(lesson_id: str, body: ChangeRoomRequest) -> LessonOut:
    student, lesson, occurrence_date = _require_occurrence(lesson_id)
    _overrides.setdefault(lesson_id, {})["room"] = body.room
    return _lesson_out(student, lesson, occurrence_date)


@app.post("/mock/reset", status_code=204)
def reset() -> None:
    _overrides.clear()


@app.post("/mock/restore-lesson/{lesson_id}", response_model=LessonOut)
def restore_lesson(lesson_id: str) -> LessonOut:
    student, lesson, occurrence_date = _require_occurrence(lesson_id)
    _overrides.pop(lesson_id, None)
    return _lesson_out(student, lesson, occurrence_date)


@app.get("/mock/lessons", response_model=list[LessonOut])
def list_lessons(student: str | None = Query(default=None)) -> list[LessonOut]:
    """Upcoming lessons for one student (default: the demo student), so the /demo page needs no login."""
    today = datetime.now(SAST).date()
    lessons = _materialize(_student_or_404(student), today, today + timedelta(days=14))
    return sorted(lessons, key=lambda lesson: lesson.start_ts)


def _student_out(key: str, student: dict) -> StudentOut:
    return StudentOut(
        key=key,
        user=student["user"],
        display_name=student["display_name"],
        class_name=student.get("class_name"),
        guest=student.get("guest", False),
    )


@app.get("/mock/students", response_model=list[StudentOut])
def list_students() -> list[StudentOut]:
    """Everyone the /demo page can switch between: demo student, developers, then guests."""
    return [_student_out(key, student) for key, student in _students.items()]


_GUEST_TEMPLATE_BASE = 100_000  # fixture ids stay below this, so guest lesson ids never collide
_GUEST_SLOTS = [("08:00", "09:30"), ("10:00", "11:30"), ("13:00", "14:30"), ("14:45", "16:15")]
_GUEST_ROOMS = ["A04", "B12", "Lab 1", "Lab 2", "C07", "D15"]
_GUEST_LECTURERS = ["Dr A. Naidoo", "Mr T. Botha", "Ms L. Dlamini", "Dr P. van Wyk"]
_guest_count = 0


def _build_guest(first_name: str, last_name: str) -> dict:
    """A throwaway student: random timetable over the demo student's subjects, ids of its own."""
    global _guest_count
    _guest_count += 1
    number = _guest_count
    rng = random.Random(number)
    template = next(iter(_students.values()))
    slug = re.sub(r"[^a-z0-9]+", ".", f"{first_name}.{last_name}".lower()).strip(".") or "guest"
    lessons = []
    taken: set[tuple[int, int]] = set()
    while len(lessons) < 8:
        weekday, slot = rng.randrange(5), rng.randrange(len(_GUEST_SLOTS))
        if (weekday, slot) in taken:
            continue
        taken.add((weekday, slot))
        start, end = _GUEST_SLOTS[slot]
        lessons.append({
            "id": _GUEST_TEMPLATE_BASE + number * 100 + len(lessons),
            "subject_id": rng.choice(template["subjects"])["id"],
            "weekday": weekday,
            "start_time": start,
            "end_time": end,
            "room": rng.choice(_GUEST_ROOMS),
            "status": "scheduled",
            "lecturer": rng.choice(_GUEST_LECTURERS),
            "group": f"Group {rng.randint(1, 2)}",
        })
    return {
        "server": template["server"],
        "school": template["school"],
        "user": f"{slug}.{secrets.token_hex(2)}",  # random suffix: a mock restart cannot reissue a name,
        "key": f"GUEST-{secrets.token_hex(6)}",
        "display_name": f"{first_name} {last_name}",
        "block_start": template["block_start"],
        "class_name": template.get("class_name"),
        "subjects": template["subjects"],
        "lessons": lessons,
        "guest": True,
    }


@app.post("/mock/guests", response_model=StudentOut, status_code=201)
def create_guest(body: GuestRequest) -> StudentOut:
    """Mints a guest on the spot — a name is all it takes (issue #129). Held in memory only."""
    guest = _build_guest(body.first_name, body.last_name)
    key = student_key(guest["school"], guest["user"])
    _students[key] = guest
    return _student_out(key, guest)


@app.post("/mock/nudge")
def nudge() -> JSONResponse:
    """Forwards to the worker's POST /nudge. The worker's own LAN check still applies."""
    request = urllib.request.Request(_WORKER_NUDGE_URL, method="POST")
    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            return JSONResponse(json.loads(response.read() or "null"), status_code=response.status)
    except urllib.error.HTTPError as error:
        raise HTTPException(status_code=error.code, detail=f"worker answered {error.code}") from error
    except (urllib.error.URLError, OSError, ValueError) as error:
        raise HTTPException(status_code=502, detail=f"worker unreachable: {error}") from error


@app.get("/demo", include_in_schema=False)
def demo_page() -> FileResponse:
    return FileResponse(_DEMO_PAGE, media_type="text/html")
