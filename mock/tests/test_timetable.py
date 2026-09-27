LESSON_FIELDS = {"untis_lesson_id", "subject_code", "subject_name", "room", "status", "start_ts", "end_ts"}


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
