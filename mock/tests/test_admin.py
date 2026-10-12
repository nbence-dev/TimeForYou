import json

from app.fixtures import FIXTURES_DIR

# A fixed week, so the dates the tests below hard-code always contain the lesson they pick. Using
# "this week" broke them once the calendar moved past 2026-10-11.
FIXED_WEEK = {"start": "2026-09-28", "end": "2026-10-04"}


def _first_lesson_id(client, auth_headers):
    lessons = client.get("/timetable", headers=auth_headers, params=FIXED_WEEK).json()
    return lessons[0]["untis_lesson_id"]


def test_cancel_lesson_only_affects_that_occurrence(client, auth_headers):
    lesson_id = _first_lesson_id(client, auth_headers)

    response = client.post(f"/mock/cancel-lesson/{lesson_id}")

    assert response.status_code == 200
    assert response.json()["status"] == "cancelled"

    lessons = client.get(
        "/timetable",
        headers=auth_headers,
        params={"start": "2026-09-28", "end": "2026-10-11"},
    ).json()
    matching = [lesson for lesson in lessons if lesson["untis_lesson_id"] == lesson_id]
    assert matching[0]["status"] == "cancelled"

    template_id = lesson_id.split("-", 1)[0]

    def same_template_other_occurrence(other_id: str) -> bool:
        return other_id.split("-", 1)[0] == template_id and other_id != lesson_id

    other_week = [
        lesson for lesson in lessons if same_template_other_occurrence(lesson["untis_lesson_id"])
    ]
    assert other_week
    assert all(lesson["status"] != "cancelled" for lesson in other_week)


def test_move_lesson_keeps_id_and_updates_time(client, auth_headers):
    lesson_id = _first_lesson_id(client, auth_headers)
    new_start = "2026-09-28T15:00:00+02:00"
    new_end = "2026-09-28T16:30:00+02:00"

    response = client.post(
        f"/mock/move-lesson/{lesson_id}",
        json={"start_ts": new_start, "end_ts": new_end},
    )

    assert response.status_code == 200
    body = response.json()
    assert body["untis_lesson_id"] == lesson_id
    assert body["start_ts"] == new_start
    assert body["end_ts"] == new_end

    lessons = client.get(
        "/timetable",
        headers=auth_headers,
        params={"start": "2026-09-28", "end": "2026-10-04"},
    ).json()
    matching = next(lesson for lesson in lessons if lesson["untis_lesson_id"] == lesson_id)
    assert matching["start_ts"] == new_start


def test_move_lesson_outside_range_is_excluded_from_that_range(client, auth_headers):
    # untis_lesson_id is minted from the lesson's *original* occurrence day, so a move that lands
    # outside the queried window must not surface a row whose displayed start_ts is out of range.
    lesson_id = _first_lesson_id(client, auth_headers)

    client.post(
        f"/mock/move-lesson/{lesson_id}",
        json={"start_ts": "2026-10-12T15:00:00+02:00", "end_ts": "2026-10-12T16:30:00+02:00"},
    )

    lessons = client.get(
        "/timetable",
        headers=auth_headers,
        params={"start": "2026-09-28", "end": "2026-10-04"},
    ).json()
    assert lesson_id not in [lesson["untis_lesson_id"] for lesson in lessons]


def test_change_room(client, auth_headers):
    lesson_id = _first_lesson_id(client, auth_headers)

    response = client.post(f"/mock/change-room/{lesson_id}", json={"room": "Z99"})

    assert response.status_code == 200
    assert response.json()["room"] == "Z99"


def test_reset_restores_fixture_state(client, auth_headers):
    lesson_id = _first_lesson_id(client, auth_headers)
    client.post(f"/mock/cancel-lesson/{lesson_id}")

    response = client.post("/mock/reset")

    assert response.status_code == 204
    lessons = client.get("/timetable", headers=auth_headers, params=FIXED_WEEK).json()
    matching = next(lesson for lesson in lessons if lesson["untis_lesson_id"] == lesson_id)
    assert matching["status"] == "scheduled"


def test_reset_leaves_fixture_files_untouched(client, auth_headers):
    fixture_path = next(FIXTURES_DIR.glob("*.json"))
    original = fixture_path.read_text()
    lesson_id = _first_lesson_id(client, auth_headers)

    client.post(f"/mock/cancel-lesson/{lesson_id}")
    client.post(
        f"/mock/move-lesson/{lesson_id}",
        json={"start_ts": "2026-09-28T15:00:00+02:00", "end_ts": "2026-09-28T16:30:00+02:00"},
    )
    client.post(f"/mock/change-room/{lesson_id}", json={"room": "Z99"})
    client.post("/mock/reset")

    assert fixture_path.read_text() == original
    json.loads(original)  # sanity: still valid JSON


def test_admin_endpoints_404_on_unknown_id(client):
    assert client.post("/mock/cancel-lesson/9999-2026-09-28").status_code == 404
    assert client.post("/mock/change-room/9999-2026-09-28", json={"room": "A1"}).status_code == 404
    assert (
        client.post(
            "/mock/move-lesson/9999-2026-09-28",
            json={"start_ts": "2026-09-28T15:00:00+02:00", "end_ts": "2026-09-28T16:30:00+02:00"},
        ).status_code
        == 404
    )
    assert client.post("/mock/cancel-lesson/not-a-valid-id").status_code == 404


def test_timetable_multi_week_range_has_no_duplicate_ids(client, auth_headers):
    lessons = client.get(
        "/timetable",
        headers=auth_headers,
        params={"start": "2026-09-28", "end": "2026-10-11"},
    ).json()

    assert len(lessons) == 16  # 8 lessons/week * 2 weeks
    ids = [lesson["untis_lesson_id"] for lesson in lessons]
    assert len(ids) == len(set(ids))
