DEVS = {"nbence-dev", "DogCE1", "stefanroux501", "MardoSaayman", "MarcusS1221", "StephanEsterhuizen"}


def _students(client):
    return client.get("/mock/students").json()


def _by_user(client, user):
    return next(s for s in _students(client) if s["user"] == user)


def _lessons(client, key):
    return client.get("/mock/lessons", params={"student": key}).json()


def test_lists_demo_student_first_then_one_per_developer(client):
    students = _students(client)

    assert students[0]["user"] == "demo.student"
    assert DEVS <= {s["user"] for s in students}


def test_each_student_has_a_timetable_different_from_the_others(client):
    shapes = {
        tuple(
            (lesson["subject_code"], lesson["room"], lesson["start_ts"][:16])
            for lesson in _lessons(client, s["key"])
        )
        for s in _students(client)
    }

    assert len(shapes) == len(_students(client))


def test_lesson_ids_are_unique_across_students(client):
    ids = [lesson["untis_lesson_id"] for s in _students(client) for lesson in _lessons(client, s["key"])]

    assert len(ids) == len(set(ids))


def test_cancelling_for_one_student_leaves_the_others_untouched(client):
    first, second = _students(client)[:2]
    target = _lessons(client, first["key"])[0]["untis_lesson_id"]

    client.post(f"/mock/cancel-lesson/{target}")

    assert any(lesson["status"] == "cancelled" for lesson in _lessons(client, first["key"]))
    assert all(lesson["status"] != "cancelled" for lesson in _lessons(client, second["key"]))


def test_each_student_has_a_scannable_qr_that_logs_in_as_them(client):
    for student in _students(client):
        payload = client.get("/qr-payload", params={"student": student["key"]}).json()
        assert payload["user"] == student["user"]
        assert client.post("/login", json=payload).status_code == 200
        png = client.get("/qr-code", params={"student": student["key"]})
        assert png.headers["content-type"] == "image/png"


def test_unknown_student_is_404(client):
    assert client.get("/qr-code", params={"student": "nope:nobody"}).status_code == 404
    assert client.get("/mock/lessons", params={"student": "nope:nobody"}).status_code == 404


def test_profile_shows_the_students_own_name(client):
    dev = _by_user(client, "DogCE1")
    payload = client.get("/qr-payload", params={"student": dev["key"]}).json()
    session = client.post("/login", json=payload).json()["session_id"]

    profile = client.get("/profile", headers={"Authorization": f"Bearer {session}"}).json()

    assert profile["display_name"] == "DogCE1"


def test_creating_a_guest_gives_a_working_login_and_their_own_timetable(client):
    response = client.post("/mock/guests", json={"first_name": "Jane", "last_name": "Doe"})

    assert response.status_code == 201
    guest = response.json()
    assert guest["display_name"] == "Jane Doe"
    payload = client.get("/qr-payload", params={"student": guest["key"]}).json()
    assert client.post("/login", json=payload).status_code == 200
    lessons = _lessons(client, guest["key"])
    assert lessons
    others = [s for s in _students(client) if s["key"] != guest["key"]]
    other_ids = {lesson["untis_lesson_id"] for s in others for lesson in _lessons(client, s["key"])}
    assert not other_ids & {lesson["untis_lesson_id"] for lesson in lessons}


def test_two_guests_with_the_same_name_get_distinct_accounts(client):
    a = client.post("/mock/guests", json={"first_name": "Sam", "last_name": "Lee"}).json()
    b = client.post("/mock/guests", json={"first_name": "Sam", "last_name": "Lee"}).json()

    assert a["key"] != b["key"]
    assert a["user"] != b["user"]


def test_guest_names_must_not_be_blank(client):
    assert client.post("/mock/guests", json={"first_name": " ", "last_name": "Doe"}).status_code == 422
    assert client.post("/mock/guests", json={"first_name": "Jane", "last_name": ""}).status_code == 422


def test_cancelling_a_guest_lesson_only_changes_that_guest(client):
    guest = client.post("/mock/guests", json={"first_name": "Ann", "last_name": "Fox"}).json()
    other = _students(client)[0]
    target = _lessons(client, guest["key"])[0]["untis_lesson_id"]

    client.post(f"/mock/cancel-lesson/{target}")

    assert any(lesson["status"] == "cancelled" for lesson in _lessons(client, guest["key"]))
    assert all(lesson["status"] != "cancelled" for lesson in _lessons(client, other["key"]))
