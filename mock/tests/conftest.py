import pytest
from fastapi.testclient import TestClient

from app.main import _overrides, _students, app


@pytest.fixture(autouse=True)
def _reset_overrides():
    _overrides.clear()
    yield
    _overrides.clear()
    for key in [k for k, s in _students.items() if s.get("guest")]:
        del _students[key]  # guests are per-test, like the overrides


@pytest.fixture()
def client() -> TestClient:
    return TestClient(app)


@pytest.fixture()
def auth_headers(client: TestClient) -> dict[str, str]:
    payload = client.get("/qr-payload").json()
    session_id = client.post("/login", json=payload).json()["session_id"]
    return {"Authorization": f"Bearer {session_id}"}
