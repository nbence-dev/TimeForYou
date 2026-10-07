import { toCardStatus } from '@/shared/components/to-card-status';
import type { CardStatus } from '@/shared/theme/theme';
import type { Lesson } from '@/shared/types/lesson';

// Pure helpers for the Today screen: no React, no I/O.
//
// Days are the student's own calendar days (device local time), not UTC: a lesson at 00:30 belongs
// to the day the student sees on their clock. A day is named by its `YYYY-MM-DD` key.

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

const pad = (n: number) => String(n).padStart(2, '0');

export function dayKey(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function parseDayKey(key: string): Date {
  const [year, month, day] = key.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function shiftDay(key: string, days: number): string {
  const date = parseDayKey(key);
  date.setDate(date.getDate() + days);
  return dayKey(date);
}

/** "Monday, 5 Oct". */
export function formatDayTitle(key: string): string {
  const date = parseDayKey(key);
  return `${WEEKDAYS[date.getDay()]}, ${date.getDate()} ${MONTHS[date.getMonth()]}`;
}

/** "08:00", in the device's local time. */
export function formatTime(timestamp: string | Date): string {
  const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/**
 * The local day a lesson starts on, or `null` if its start or end time cannot be parsed. A lesson
 * with a bad timestamp is left out entirely: a bad start would become the day `"NaN-NaN-NaN"`
 * (sorting after every real one and breaking navigation), and a bad end would leave the lesson
 * "still to come" forever and show `NaN:NaN`.
 */
function startDay(lesson: Lesson): string | null {
  const start = new Date(lesson.start_ts);
  if (Number.isNaN(start.getTime()) || Number.isNaN(Date.parse(lesson.end_ts))) return null;
  return dayKey(start);
}

/** Groups lessons by local day; each day's lessons are earliest first. */
export function lessonsByDay(lessons: readonly Lesson[]): Map<string, Lesson[]> {
  const byDay = new Map<string, Lesson[]>();
  for (const lesson of lessons) {
    const key = startDay(lesson);
    if (key === null) continue;
    const day = byDay.get(key);
    if (day) day.push(lesson);
    else byDay.set(key, [lesson]);
  }
  for (const day of byDay.values()) {
    day.sort((a, b) => Date.parse(a.start_ts) - Date.parse(b.start_ts));
  }
  return byDay;
}

/**
 * The days the student can step between: from the earliest to the latest day that has a lesson,
 * widened to include `today`. Taken from the data, so it is whatever window the server returned
 * and never a length hard-coded in the app.
 */
export function dayRange(lessons: readonly Lesson[], today: string): { first: string; last: string } {
  let first = today;
  let last = today;
  for (const lesson of lessons) {
    const key = startDay(lesson);
    if (key === null) continue;
    if (key < first) first = key;
    if (key > last) last = key;
  }
  return { first, last };
}

/**
 * Keeps a chosen day inside the days that exist. After a refresh the range can shrink (the window
 * rolls forward, or the cache is replaced), and a day left outside it would show an empty page.
 */
export function clampDay(day: string, range: { first: string; last: string }): string {
  if (day < range.first) return range.first;
  if (day > range.last) return range.last;
  return day;
}

export type LessonPhase = 'past' | 'live' | 'upcoming';

export function lessonPhase(lesson: Lesson, now: Date): LessonPhase {
  const time = now.getTime();
  if (time >= Date.parse(lesson.end_ts)) return 'past';
  if (time >= Date.parse(lesson.start_ts)) return 'live';
  return 'upcoming';
}

/**
 * How a lesson's card looks. A cancelled lesson stays cancelled even while its slot is running;
 * otherwise the lesson happening now is "live", and everything else follows the API status.
 */
export function cardStatusFor(lesson: Lesson, phase: LessonPhase): CardStatus {
  if (lesson.status === 'cancelled') return 'cancelled';
  if (phase === 'live') return 'live';
  return toCardStatus(lesson.status);
}
