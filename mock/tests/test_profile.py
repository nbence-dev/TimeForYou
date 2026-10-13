def test_profile_requires_auth(client):
    response = client.get("/profile")

    assert response.status_code == 401


def test_profile_returns_the_students_name_and_class(client, auth_headers):
    response = client.get("/profile", headers=auth_headers)

    assert response.status_code == 200
    assert response.json() == {"display_name": "Demo Student", "class_name": "ITMDA3-B34"}