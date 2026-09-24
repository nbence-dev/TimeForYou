def test_login_succeeds_with_seeded_credentials(client):
    payload = client.get("/qr-payload").json()

    response = client.post("/login", json=payload)

    assert response.status_code == 200
    assert "session_id" in response.json()


def test_login_rejects_wrong_key(client):
    payload = client.get("/qr-payload").json()
    payload["key"] = "wrong-key"

    response = client.post("/login", json=payload)

    assert response.status_code == 401


def test_login_rejects_unknown_user(client):
    payload = client.get("/qr-payload").json()
    payload["user"] = "not.a.real.student"

    response = client.post("/login", json=payload)

    assert response.status_code == 401
