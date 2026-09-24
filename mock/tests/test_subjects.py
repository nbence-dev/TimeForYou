def test_subjects_requires_auth(client):
    response = client.get("/subjects")

    assert response.status_code == 401


def test_subjects_returns_seeded_subjects(client, auth_headers):
    response = client.get("/subjects", headers=auth_headers)

    assert response.status_code == 200
    subjects = response.json()
    assert len(subjects) >= 1
    assert {"id", "code", "name"} <= subjects[0].keys()
