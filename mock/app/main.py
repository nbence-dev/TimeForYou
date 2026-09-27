"""Mock WebUntis — the system's only backend (issue #3 § Architecture; PROJECT-BRIEF.md §0a.1).

Plain REST, not WebUntis's own protocol: there is no real WebUntis tenant reachable and no Node
sidecar translating for one, so `IWebUntisClient` (server/TimeForYou.WebUntis) will call this
service directly. Read surface only — auth, timetable, subjects, and QR-code generation for the
onboarding demo. The unauthenticated admin mutation endpoints (`/mock/cancel-lesson/{id}` etc.)
are a separate, later ticket.
"""

import io
import json
import uuid
from datetime import date, datetime, time, timedelta, timezone

import qrcode
from fastapi import FastAPI, Header, HTTPException, Query
from fastapi.responses import StreamingResponse

from app.fixtures import load_students, student_key
from app.schemas import LessonOut, LoginRequest, LoginResponse, QrPayload, SubjectOut

# Eduvos is a South African institution; SAST has no DST, so a fixed UTC+2 offset is always correct.
SAST = timezone(timedelta(hours=2))

app = FastAPI(title="TimeForYou Mock WebUntis")

_students = load_students()
_sessions: dict[str, str] = {}  # session_id -> student key


def _demo_student() -> dict:
    return next(iter(_students.values()))


def _authenticate(authorization: str | None) -> dict:
    if authorization is None or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="missing bearer session")
    session_id = authorization.removeprefix("Bearer ")
    key = _sessions.get(session_id)
    if key is None:
        raise HTTPException(status_code=401, detail="unknown or expired session")
    return _students[key]


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


def _materialize(student: dict, anchor_monday: date) -> list[LessonOut]:
    """Projects the fixture's weekly template onto real calendar dates for the given week.

    Lessons stay demoable regardless of when the mock is run — the seeded week is always
    "this week" rather than a fixed date that goes stale before the showcase.
    """
    subjects_by_id = {s["id"]: s for s in student["subjects"]}
    lessons = []
    for lesson in student["lessons"]:
        lesson_date = anchor_monday + timedelta(days=lesson["weekday"])
        subject = subjects_by_id[lesson["subject_id"]]
        lessons.append(
            LessonOut(
                untis_lesson_id=lesson["id"],
                subject_code=subject["code"],
                subject_name=subject["name"],
                room=lesson["room"],
                status=lesson["status"],
                start_ts=datetime.combine(lesson_date, time.fromisoformat(lesson["start_time"]), tzinfo=SAST),
                end_ts=datetime.combine(lesson_date, time.fromisoformat(lesson["end_time"]), tzinfo=SAST),
            )
        )
    return lessons


@app.get("/timetable", response_model=list[LessonOut])
def get_timetable(
    authorization: str | None = Header(default=None),
    start: date | None = Query(default=None),
    end: date | None = Query(default=None),
) -> list[LessonOut]:
    student = _authenticate(authorization)
    lessons = _materialize(student, _week_monday(start or datetime.now(SAST).date()))
    if start is not None:
        lessons = [lesson for lesson in lessons if lesson.start_ts.date() >= start]
    if end is not None:
        lessons = [lesson for lesson in lessons if lesson.start_ts.date() <= end]
    return lessons


@app.get("/subjects", response_model=list[SubjectOut])
def get_subjects(authorization: str | None = Header(default=None)) -> list[SubjectOut]:
    student = _authenticate(authorization)
    return [SubjectOut(**subject) for subject in student["subjects"]]


@app.get("/qr-payload", response_model=QrPayload)
def qr_payload() -> QrPayload:
    student = _demo_student()
    return QrPayload(
        server=student["server"],
        school=student["school"],
        user=student["user"],
        key=student["key"],
    )


@app.get("/qr-code")
def qr_code() -> StreamingResponse:
    """A real, scannable QR image — the demo operator opens this in a browser and a phone scans
    it directly, the same shape the app expects from a genuine WebUntis SSO QR code."""
    payload = qr_payload()
    image = qrcode.make(json.dumps(payload.model_dump()))
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="image/png")

