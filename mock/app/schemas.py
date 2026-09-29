from datetime import datetime

from pydantic import BaseModel


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
    room: str
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
