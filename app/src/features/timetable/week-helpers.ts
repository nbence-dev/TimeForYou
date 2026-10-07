import type { Lesson } from '@/shared/types/lesson';

import { dayKey, shiftDay } from './lesson-time';

// Pure week helpers for the Timetable screen (#60): no React, no I/O. Weeks run Monday to Sunday in
// the device's local time, using the same `YYYY-MM-DD` day keys as lesson-time.ts.

const SHORT_WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const LONG_WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function parseDayKey(key: string): Date {
  const [year, month, day] = key.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export type DayRange = { first: string; last: string };

/** The Monday of the week `key` falls in. */
export function mondayOf(key: string): string {
  const sinceMonday = (parseDayKey(key).getDay() + 6) % 7;
  return shiftDay(key, -sinceMonday);
}

/** Monday to Sunday of the week starting on `monday`. */
export function daysOfWeek(monday: string): string[] {
  return Array.from({ length: 7 }, (_, i) => shiftDay(monday, i));
}

/**
 * Every week (named by its Monday) from the week of `range.first` to the week of `range.last`. The
 * range comes from the lessons the API returned, so this is however many weeks the server serves.
 */
export function weeksIn(range: DayRange): string[] {
  const weeks: string[] = [];
  for (let monday = mondayOf(range.first); monday <= range.last; monday = shiftDay(monday, 7)) {
    weeks.push(monday);
  }
  return weeks;
}

export function isWithin(key: string, range: DayRange): boolean {
  return key >= range.first && key <= range.last;
}

export function clampDay(key: string, range: DayRange): string {
  if (key < range.first) return range.first;
  if (key > range.last) return range.last;
  return key;
}

export const shortWeekday = (key: string) => SHORT_WEEKDAYS[parseDayKey(key).getDay()];
export const longWeekday = (key: string) => LONG_WEEKDAYS[parseDayKey(key).getDay()];
export const dayOfMonth = (key: string) => parseDayKey(key).getDate();

/** The local day a lesson starts on, or `null` for an unparseable `start_ts`. */
export function lessonDay(lesson: Lesson): string | null {
  const start = new Date(lesson.start_ts);
  return Number.isNaN(start.getTime()) ? null : dayKey(start);
}

/** The first lesson that isn't cancelled on a day after `day`. `lessons` must be earliest first. */
export function nextLessonAfter(lessons: readonly Lesson[], day: string): Lesson | null {
  for (const lesson of lessons) {
    if (lesson.status === 'cancelled') continue;
    const key = lessonDay(lesson);
    if (key !== null && key > day) return lesson;
  }
  return null;
}

/** "30m", "1h", "1h 15m". */
export function formatGap(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return `${rest}m`;
  return rest === 0 ? `${hours}h` : `${hours}h ${rest}m`;
}