"""Loads seeded student fixtures from data files under mock/fixtures/.

Fixtures are plain JSON, not hardcoded in Python, so the demo dataset can be edited without
touching this module (issue #17 acceptance criteria).
"""

import json
from pathlib import Path

FIXTURES_DIR = Path(__file__).resolve().parent.parent / "fixtures"


def student_key(school: str, user: str) -> str:
    return f"{school}:{user}"


def load_students() -> dict[str, dict]:
    """Returns seeded students keyed by "{school}:{user}", one entry per fixture file."""
    students: dict[str, dict] = {}
    for path in sorted(FIXTURES_DIR.glob("*.json")):
        data = json.loads(path.read_text())
        students[student_key(data["school"], data["user"])] = data
    return students
