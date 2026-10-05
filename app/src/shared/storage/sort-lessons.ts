import type { Lesson } from "@/shared/types/lesson";

/**
 * Earliest start first. Compares real instants rather than the strings, so two backends that store
 * the same lessons always return them in the same order, whatever UTC offset the server wrote.
 */
export function sortLessonsByStart(lessons: Lesson[]): Lesson[] {
  return lessons.sort((a, b) => Date.parse(a.start_ts) - Date.parse(b.start_ts));
}
