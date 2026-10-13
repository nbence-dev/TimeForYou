import type { ChangeLogEntry } from "@/shared/api/types";

import { kvStore } from "./kv-store";

/**
 * What the app remembers about changes between launches (#72): the last list it fetched, so the
 * markers work offline, the ids of the changes the student has seen, and the ids they have archived
 * with "Mark all as read" (#138). All of it belongs to whoever is signed in, so `clear()` runs when
 * that changes hands.
 */

const CACHE_KEY = "changes.cache";
const SEEN_KEY = "changes.seenIds";
const ARCHIVED_KEY = "changes.archivedIds";

/**
 * Every write and the wipe go through one queue, in the order they were asked for. Without it a slow
 * write could finish after `clear()` and bring the previous student's data back.
 */
let queue: Promise<unknown> = Promise.resolve();
function enqueue<T>(task: () => Promise<T>): Promise<T> {
  const run = queue.then(task, task);
  queue = run.catch(() => undefined);
  return run;
}

function isChange(value: unknown): value is ChangeLogEntry {
  if (typeof value !== "object" || value === null) return false;
  const entry = value as Record<string, unknown>;
  return typeof entry.id === "string" && typeof entry.lesson_id === "string" && typeof entry.detected_at === "string";
}

export const changesStore = {
  /** The last fetched list; empty if there is none or it cannot be read. */
  async readCached(): Promise<ChangeLogEntry[]> {
    try {
      const raw = await kvStore.get(CACHE_KEY);
      const parsed: unknown = raw === null ? [] : JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter(isChange) : [];
    } catch {
      return [];
    }
  },

  writeCached(changes: readonly ChangeLogEntry[]): Promise<void> {
    return enqueue(() => kvStore.set(CACHE_KEY, JSON.stringify(changes)));
  },

  /**
   * The ids of the changes the student has seen, or `null` if none have ever been saved (the first
   * launch). Rejects if the store itself cannot be read: that must not look like a first launch, or
   * every unseen change would be treated as seen.
   */
  async readSeenIds(): Promise<string[] | null> {
    const raw = await kvStore.get(SEEN_KEY);
    if (raw === null) return null;
    try {
      const parsed: unknown = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter((id): id is string => typeof id === "string") : null;
    } catch {
      return null;
    }
  },

  writeSeenIds(ids: readonly string[]): Promise<void> {
    return enqueue(() => kvStore.set(SEEN_KEY, JSON.stringify(ids)));
  },

  /** The ids of the archived changes; empty if there are none or they cannot be read. */
  async readArchivedIds(): Promise<string[]> {
    try {
      const raw = await kvStore.get(ARCHIVED_KEY);
      const parsed: unknown = raw === null ? [] : JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter((id): id is string => typeof id === "string") : [];
    } catch {
      return [];
    }
  },

  writeArchivedIds(ids: readonly string[]): Promise<void> {
    return enqueue(() => kvStore.set(ARCHIVED_KEY, JSON.stringify(ids)));
  },

  /** Forgets everything. Each key is removed on its own, so one failing cannot leave the others. */
  clear(): Promise<void> {
    return enqueue(async () => {
      await Promise.allSettled([kvStore.remove(CACHE_KEY), kvStore.remove(SEEN_KEY), kvStore.remove(ARCHIVED_KEY)]);
    });
  },
};
