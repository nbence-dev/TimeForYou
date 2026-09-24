import pytest
from fastapi.testclient import TestClient

from app.main import app


@pytest.fixture()
def client() -> TestClient:
    return TestClient(app)


@pytest.fixture()
def auth_headers(client: TestClient) -> dict[str, str]:
    payload = client.get("/qr-payload").json()
    session_id = client.post("/login", json=payload).json()["session_id"]
    return {"Authorization": f"Bearer {session_id}"}
