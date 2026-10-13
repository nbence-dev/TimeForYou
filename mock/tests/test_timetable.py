LESSON_FIELDS = {
    "untis_lesson_id", "subject_code", "subject_name", "room", "lecturer", "group", "mode",
    "status", "start_ts", "end_ts",
}


def test_timetable_requires_auth(client):
    response = client.get("/timetable")

    assert response.status_code == 401


def test_timetable_returns_seeded_lessons(client, auth_headers):
    response = client.get("/timetable", headers=auth_headers)

    assert response.status_code == 200
    lessons = response.json()
    assert len(lessons) >= 5
    assert LESSON_FIELDS <= lessons[0].keys()


def test_timetable_lesson_ids_are_unique(client, auth_headers):
    lessons = client.get("/timetable", headers=auth_headers).json()

    ids = [lesson["untis_lesson_id"] for lesson in lessons]
    assert len(ids) == len(set(ids))


def test_timetable_filters_by_date_range(client, auth_headers):
    all_lessons = client.get("/timetable", headers=auth_headers).json()
    target_date = all_lessons[0]["start_ts"][:10]

    response = client.get(
        "/timetable", headers=auth_headers, params={"start": target_date, "end": target_date}
    )

    assert response.status_code == 200
    for lesson in response.json():
        assert lesson["start_ts"].startswith(target_date)


def test_timetable_lessons_carry_lecturer_group_and_mode(client, auth_headers):
    lessons = client.get("/timetable", headers=auth_headers).json()

    for lesson in lessons:
        assert lesson["lecturer"]
        assert lesson["group"]
        assert lesson["mode"] in {"online", "in_person"}


def test_online_lessons_have_no_room_and_in_person_lessons_do(client, auth_headers):
    lessons = client.get("/timetable", headers=auth_headers).json()

    for lesson in lessons:
        assert (lesson["room"] is None) == (lesson["mode"] == "online")


def test_weeks_alternate_between_online_and_in_person(client, auth_headers):
    def mode_in_week(start):
        params = {"start": start, "end": start}
        lessons = client.get("/timetable", headers=auth_headers, params=params).json()
        return {lesson["mode"] for lesson in lessons}

    # block_start is Monday 2026-10-12, an online week; the following Monday is in-person.
    assert mode_in_week("2026-10-12") == {"online"}
    assert mode_in_week("2026-10-19") == {"in_person"}
    assert mode_in_week("2026-10-26") == {"online"}
