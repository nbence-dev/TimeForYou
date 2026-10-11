import type { ChangeLogEntry, Lesson } from '@/shared/api';

// Pure helpers for the Changes screen (#71): no React, no I/O. Times are the device's local time.

/** The kinds this screen lists. `added` and `removed` have no old and new value to show. */
export type ShownChangeType = 'cancelled' | 'room' | 'time';
export type ShownChange = ChangeLogEntry & { change_type: ShownChangeType };

// The grouped notification's headline order: cancellations first, then room, then time.
const ORDER: Record<ShownChangeType, number> = { cancelled: 0, room: 1, time: 2 };

function isShown(change: ChangeLogEntry): change is ShownChange {
  return change.change_type in ORDER;
}

function instant(timestamp: string): number {
  const ms = Date.parse(timestamp);
  return Number.isNaN(ms) ? 0 : ms;
}

/** Newest first. Changes found in the same poll keep the cancelled, room, time order. */
export function sortChanges(changes: readonly ChangeLogEntry[]): ShownChange[] {
  return changes
    .filter(isShown)
    .sort(
      (a, b) =>
        instant(b.detected_at) - instant(a.detected_at) ||
        ORDER[a.change_type] - ORDER[b.change_type],
    );
}

export function countByType(changes: readonly ShownChange[]): Record<ShownChangeType, number> {
  const counts: Record<ShownChangeType, number> = { cancelled: 0, room: 0, time: 0 };
  for (const change of changes) counts[change.change_type] += 1;
  return counts;
}

/** A cancellation. The same change type also records a lesson coming back (`cancelled` → `scheduled`). */
export function isCancellation(change: ShownChange): boolean {
  return change.change_type === 'cancelled' && change.new_value === 'cancelled';
}

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

const pad = (n: number) => String(n).padStart(2, '0');

function parse(timestamp: string | null | undefined): Date | null {
  if (!timestamp) return null;
  const date = new Date(timestamp);
  return Number.isNaN(date.getTime()) ? null : date;
}

/** "08:00". */
export function formatTime(date: Date): string {
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** "Wed, 14 Oct". */
function formatDay(date: Date): string {
  return `${WEEKDAYS[date.getDay()]}, ${date.getDate()} ${MONTHS[date.getMonth()]}`;
}

/** A time change stores the slot as "start/end", both ISO-8601. */
function parseSlot(value: string | null | undefined): [Date, Date] | null {
  const [start, end] = (value ?? '').split('/').map(parse);
  return start && end ? [start, end] : null;
}

function formatSlot([start, end]: [Date, Date], withDay = true): string {
  const times = `${formatTime(start)} – ${formatTime(end)}`;
  return withDay ? `${formatDay(start)} · ${times}` : times;
}

/** When the lesson is, as it stands now: "Wed, 14 Oct · 12:00 – 13:00". Null if unknown. */
export function describeWhen(change: ShownChange, lesson: Lesson | undefined): string | null {
  const start = parse(lesson?.start_ts);
  const end = parse(lesson?.end_ts);
  if (start && end) return formatSlot([start, end]);
  // The lesson may have left the timetable window; a time change still says where it moved to.
  const moved = change.change_type === 'time' ? parseSlot(change.new_value) : null;
  return moved ? formatSlot(moved) : null;
}

export type ValueChange = { label: string; from: string; to: string };

function statusLabel(status: string | null | undefined): string {
  if (status === 'cancelled') return 'Cancelled';
  if (status === 'irregular') return 'Changed';
  return 'Scheduled';
}

/** The "Room: B204 → A101" row. Null for a cancellation: the chip already says it. */
export function describeValues(change: ShownChange): ValueChange | null {
  if (change.change_type === 'room') {
    return { label: 'Room', from: change.old_value || 'No room', to: change.new_value || 'No room' };
  }
  if (change.change_type === 'time') {
    const before = parseSlot(change.old_value);
    const after = parseSlot(change.new_value);
    if (!before || !after) return null;
    // Only name the day when the lesson moved to a different one.
    const withDay = before[0].toDateString() !== after[0].toDateString();
    return { label: 'Time', from: formatSlot(before, withDay), to: formatSlot(after, withDay) };
  }
  if (isCancellation(change)) return null;
  return { label: 'Status', from: statusLabel(change.old_value), to: statusLabel(change.new_value) };
}