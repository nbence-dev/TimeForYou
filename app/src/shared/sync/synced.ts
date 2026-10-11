const listeners = new Set<() => void>();

/** Calls `listener` whenever a screen has just fetched fresh data (Settings' Sync now). Returns the unsubscribe. */
export function onSynced(listener: () => void): () => void {
  listeners.add(listener);
  return () => void listeners.delete(listener);
}

/** Tells the screens that keep their own copy of the timetable and changes to fetch again. */
export function announceSynced(): void {
  listeners.forEach((listener) => listener());
}
