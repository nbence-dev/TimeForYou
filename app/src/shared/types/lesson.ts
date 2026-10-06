/** `status` in docs/openapi.yaml's Lesson schema. */
export type LessonStatus = "scheduled" | "cancelled" | "irregular";

/** docs/openapi.yaml `#/components/schemas/Lesson` — one lesson as `GET /timetable` returns it. */
export type Lesson = {
  id: string;
  untis_lesson_id: string;
  subject: string;
  lecturer?: string | null;
  room: string | null;
  /** ISO-8601 instant. */
  start_ts: string;
  /** ISO-8601 instant. */
  end_ts: string;
  status: LessonStatus;
};
