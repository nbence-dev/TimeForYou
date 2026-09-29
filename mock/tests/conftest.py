import pytest
from fastapi.testclient import TestClient

from app.main import _overrides, app


@pytest.fixture(autouse=True)
def _reset_overrides():
    _overrides.clear()
    yield
    _overrides.clear()


@pytest.fixture()
def client() -> TestClient:
    return TestClient(app)


@pytest.fixture()
def auth_headers(client: TestClient) -> dict[str, str]:
    payload = client.get("/qr-payload").json()
    session_id = client.post("/login", json=payload).json()["session_id"]
    return {"Authorization": f"Bearer {session_id}"}
