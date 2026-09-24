def test_qr_payload_shape(client):
    response = client.get("/qr-payload")

    assert response.status_code == 200
    assert set(response.json().keys()) == {"server", "school", "user", "key"}


def test_qr_code_returns_a_png_image(client):
    response = client.get("/qr-code")

    assert response.status_code == 200
    assert response.headers["content-type"] == "image/png"
    assert response.content.startswith(b"\x89PNG\r\n\x1a\n")
