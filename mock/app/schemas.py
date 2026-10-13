from datetime import datetime
from typing import Annotated, Literal

from pydantic import BaseModel, StringConstraints


class LoginRequest(BaseModel):
    server: str
    school: str
    user: str
    key: str


class LoginResponse(BaseModel):
    session_id: str


class LessonOut(BaseModel):
    untis_lesson_id: str
    subject_code: str
    subject_name: str
    room: str | None  # None while the lesson is online
    lecturer: str
    group: str
    mode: Literal["online", "in_person"]  # display-only, never diffed (issue #49)
    status: str
    start_ts: datetime
    end_ts: datetime


class SubjectOut(BaseModel):
    id: int
    code: str
    name: str


class QrPayload(BaseModel):
    server: str
    school: str
    user: str
    key: str


class MoveLessonRequest(BaseModel):
    start_ts: datetime
    end_ts: datetime


class ChangeRoomRequest(BaseModel):
    room: str

class ProfileOut(BaseModel):
    display_name: str
    class_name: str | None = None


class StudentOut(BaseModel):
    key: str  # "{school}:{user}", what the ?student= parameter takes
    user: str
    display_name: str
    class_name: str | None
    guest: bool


# Names are typed on the /demo page by the operator; anything non-blank is accepted.
Name = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=40)]


class GuestRequest(BaseModel):
    first_name: Name
    last_name: Name
