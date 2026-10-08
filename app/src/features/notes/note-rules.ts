import { ApiError, NetworkError, UnauthorizedError } from '@/shared/api/errors';

// Pure rules for notes (#73): what blocks a save, what to tell the student when a request fails, and
// how to label a note's age. No React, no I/O.

/** The API accepts 1 to 500 characters (docs/openapi.yaml `NoteWriteRequest`). */
export const MAX_NOTE_LENGTH = 500;

/** What is wrong with `text` as a note, in words for the student; `null` when it can be saved. */
export function checkNoteText(text: string): string | null {
  const body = noteBodyToSend(text);
  if (body.length === 0) return 'Write something before saving.';
  if (body.length > MAX_NOTE_LENGTH) return `A note can be at most ${MAX_NOTE_LENGTH} characters.`;
  return null;
}

/** The text that goes to the API: surrounding whitespace removed, so a blank note is empty. */
export function noteBodyToSend(text: string): string {
  return text.trim();
}

/**
 * A friendly message for a failed note request. The student's text is never lost on a failure, so
 * the messages say to try again rather than to start over.
 */
export function noteErrorMessage(error: unknown): string {
  if (error instanceof UnauthorizedError) return 'Your session has expired. Link your account again.';
  if (error instanceof NetworkError) {
    return "Couldn't reach the server. Your note is still here: try again when you're online.";
  }
  if (error instanceof ApiError) {
    if (error.status === 400) return `A note has to be between 1 and ${MAX_NOTE_LENGTH} characters.`;
    if (error.status === 404) {
      return 'That lesson or note is no longer available. It may have been removed on another device.';
    }
  }
  return 'Something went wrong saving your note. Try again.';
}

/** What to show when a lesson's notes cannot be loaded (as opposed to saved). */
export function loadErrorMessage(error: unknown): string {
  if (error instanceof NetworkError) return "Couldn't reach the server, so your notes can't be shown right now.";
  return "Couldn't load your notes. Try again.";
}

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const pad = (n: number) => String(n).padStart(2, '0');

/** "Wed 14 Oct · 10:00–11:30", in the device's local time; empty if either time is unreadable. */
export function formatLessonWhen(startTs: string, endTs: string): string {
  const start = new Date(startTs);
  const end = new Date(endTs);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return '';
  const day = `${WEEKDAYS[start.getDay()]} ${start.getDate()} ${MONTHS[start.getMonth()]}`;
  const time = (d: Date) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  return `${day} · ${time(start)}–${time(end)}`;
}

/** "just now", "5 min ago", "2h ago", "1 day ago", "3 days ago"; empty for an unreadable time. */
export function formatNoteAge(timestamp: string, now: Date): string {
  const then = Date.parse(timestamp);
  if (Number.isNaN(then)) return '';
  const seconds = Math.max(0, (now.getTime() - then) / 1000);
  if (seconds < 60) return 'just now';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return days === 1 ? '1 day ago' : `${days} days ago`;
}
