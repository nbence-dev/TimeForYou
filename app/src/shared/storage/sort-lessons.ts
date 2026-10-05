import type { Lesson } from "@/shared/types/lesson";

/** An unparseable timestamp sorts last, so the order stays deterministic on every backend. */
function instant(timestamp: string): number {
  const ms = Date.parse(timestamp);
  return Number.isNaN(ms) ? Number.POSITIVE_INFINITY : ms;
}

/**
 * Earliest start first. Compares real instants rather than the strings, so two backends that store
 * the same lessons always return them in the same order, whatever UTC offset the server wrote.
 * Returns a sorted copy; the input is left untouched.
 */
export function sortLessonsByStart(lessons: readonly Lesson[]): Lesson[] {
  return [...lessons].sort((a, b) => {
    const left = instant(a.start_ts);
    const right = instant(b.start_ts);
    if (left === right) return 0; // also covers Infinity - Infinity, which would be NaN
    return left < right ? -1 : 1;
  });
}
