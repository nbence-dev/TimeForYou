import type { Lesson } from "@/shared/types/lesson";

/**
 * The last-known timetable, kept on the device so it renders with no network (user story 8, #65).
 *
 * Callers use this and never learn which backend is underneath: IndexedDB on the web/PWA build,
 * SQLite on native. Both read and write locally only, so neither call ever waits on the network.
 */
export interface LessonCache {
  /** Every cached lesson, earliest `start_ts` first. Empty when nothing has been cached yet. */
  readLessons(): Promise<Lesson[]>;

  /**
   * Replaces everything cached with `lessons`, all or nothing: a failure part-way leaves the
   * previous contents intact, and a reader never sees a half-written timetable.
   *
   * `GET /timetable` always returns the whole window the server serves, so a fresh response
   * supersedes the cache entirely. That also drops lessons that have rolled out of the window, and
   * means a lesson that changed is overwritten rather than stored twice.
   */
  replaceLessons(lessons: readonly Lesson[]): Promise<void>;
}
