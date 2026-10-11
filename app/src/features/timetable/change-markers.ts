import type { ChangeLogEntry } from '@/shared/api/types';

// Pure rules for the changed-lesson marker (#72): which changes the student has not seen yet. No
// React, no I/O.
//
// A change is "seen" once the student has opened its lesson, or opened the Changes tab. The app
// keeps the ids of the changes seen so far: it is a set, not a point in time, so a change the
// student has not looked at stays marked across restarts until they do, and no clock (the phone's
// or the server's) decides what counts. `null` means the set is not known yet.
//
// Ids are never dropped from the set. Forgetting the ones that fell out of a fetched list would
// re-mark old changes after one short or empty response, and the saving is a few dozen ids.

/** The changes whose ids are not in `seen`. With `seen` unknown (`null`) nothing is marked. */
export function unseenChanges(
  changes: readonly ChangeLogEntry[],
  seen: ReadonlySet<string> | null,
): ChangeLogEntry[] {
  if (seen === null) return [];
  return changes.filter((change) => !seen.has(change.id));
}

/** The lessons that carry at least one unseen change (a lesson changed twice is one marker). */
export function unseenLessonIds(
  changes: readonly ChangeLogEntry[],
  seen: ReadonlySet<string> | null,
): Set<string> {
  return new Set(unseenChanges(changes, seen).map((change) => change.lesson_id));
}

/**
 * Where "seen" starts on the very first launch: everything that already exists counts as seen, so
 * a fresh install does not light up its whole history and only later changes are marked.
 */
export function initialSeenIds(changes: readonly ChangeLogEntry[]): Set<string> {
  return new Set(changes.map((change) => change.id));
}

/** `seen` plus every change on this lesson: the student has opened it. */
export function markLessonSeen(
  seen: ReadonlySet<string>,
  changes: readonly ChangeLogEntry[],
  lessonId: string,
): Set<string> {
  const next = new Set(seen);
  for (const change of changes) {
    if (change.lesson_id === lessonId) next.add(change.id);
  }
  return next;
}

/** `seen` plus every change: the student has opened the Changes tab. */
export function markAllSeen(seen: ReadonlySet<string>, changes: readonly ChangeLogEntry[]): Set<string> {
  const next = new Set(seen);
  for (const change of changes) next.add(change.id);
  return next;
}
