import type { Note } from "@/shared/api/types";

import { kvStore } from "./kv-store";

/**
 * Notes kept on the device (#124): the changes waiting to be sent, in the order they were made, and
 * the last copy of each lesson's notes fetched from the server, so notes show and can be edited
 * offline. Both belong to whoever is signed in, so `clear()` runs when that changes hands.
 */

/** One change waiting to be sent. `at` is when it was made; with `kind` and `id` it names the change. */
export type QueuedNoteChange =
  | { kind: "create"; id: string; lessonId: string; body: string; at: string }
  | { kind: "update"; id: string; lessonId: string; body: string; at: string }
  | { kind: "delete"; id: string; lessonId: string; at: string };

const QUEUE_KEY = "notes.queue";
const CACHE_KEY = "notes.cache";

/** Every write and the wipe run one after another, so a slow write can't undo `clear()`. */
let chain: Promise<unknown> = Promise.resolve();
function serial<T>(task: () => Promise<T>): Promise<T> {
  const run = chain.then(task, task);
  chain = run.catch(() => undefined);
  return run;
}

async function readJson(key: string): Promise<unknown> {
  try {
    const raw = await kvStore.get(key);
    return raw === null ? null : JSON.parse(raw);
  } catch {
    return null;
  }
}

function isQueued(value: unknown): value is QueuedNoteChange {
  if (typeof value !== "object" || value === null) return false;
  const c = value as Record<string, unknown>;
  if (typeof c.id !== "string" || typeof c.lessonId !== "string" || typeof c.at !== "string") return false;
  if (c.kind === "delete") return true;
  return (c.kind === "create" || c.kind === "update") && typeof c.body === "string";
}

function isNote(value: unknown): value is Note {
  if (typeof value !== "object" || value === null) return false;
  const n = value as Record<string, unknown>;
  return (
    typeof n.id === "string" &&
    typeof n.lesson_id === "string" &&
    typeof n.body === "string" &&
    typeof n.created_at === "string" &&
    typeof n.updated_at === "string"
  );
}

async function readQueue(): Promise<QueuedNoteChange[]> {
  const parsed = await readJson(QUEUE_KEY);
  return Array.isArray(parsed) ? parsed.filter(isQueued) : [];
}

async function readCache(): Promise<Record<string, unknown>> {
  const parsed = await readJson(CACHE_KEY);
  return typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)
    ? (parsed as Record<string, unknown>)
    : {};
}

export function sameChange(a: QueuedNoteChange, b: QueuedNoteChange): boolean {
  return a.kind === b.kind && a.id === b.id && a.at === b.at;
}

/**
 * Adds `change` to the queue, folding it into an unsent change to the same note so the queue stays
 * short: create + edit is one create with the new text, create + delete is nothing at all, and
 * edit + delete is just the delete. The head is never folded into while it is being sent.
 */
function fold(pending: QueuedNoteChange[], change: QueuedNoteChange, headInFlight: boolean): QueuedNoteChange[] {
  let index = -1;
  for (let i = pending.length - 1; i >= 0; i--) {
    if (pending[i].id === change.id) {
      index = i;
      break;
    }
  }
  if (index === -1 || (headInFlight && index === 0)) return [...pending, change];

  const earlier = pending[index];
  const next = [...pending];
  if (change.kind === "update" && earlier.kind !== "delete") {
    next[index] = { ...earlier, body: change.body };
    return next;
  }
  if (change.kind === "delete" && earlier.kind === "create") {
    next.splice(index, 1); // never reached the server, so there is nothing to delete there
    return next;
  }
  if (change.kind === "delete" && earlier.kind === "update") {
    next[index] = change;
    return next;
  }
  return [...pending, change];
}

export const notesStore = {
  /** The changes waiting to be sent, oldest first; empty if none or unreadable. */
  readQueue,

  /** Queues `change`. Pass `headInFlight` while the first queued change is being sent. */
  enqueue(change: QueuedNoteChange, headInFlight = false): Promise<void> {
    return serial(async () => {
      const pending = await readQueue();
      await kvStore.set(QUEUE_KEY, JSON.stringify(fold(pending, change, headInFlight)));
    });
  },

  /** Removes one change once it has been sent (or can never be). */
  remove(change: QueuedNoteChange): Promise<void> {
    return serial(async () => {
      const pending = await readQueue();
      const index = pending.findIndex((c) => sameChange(c, change));
      if (index === -1) return;
      pending.splice(index, 1);
      await kvStore.set(QUEUE_KEY, JSON.stringify(pending));
    });
  },

  /** The last notes fetched for a lesson, or `null` if this device has never fetched them. */
  async readLessonNotes(lessonId: string): Promise<Note[] | null> {
    const saved = (await readCache())[lessonId];
    return Array.isArray(saved) ? saved.filter(isNote) : null;
  },

  writeLessonNotes(lessonId: string, notes: readonly Note[]): Promise<void> {
    return serial(async () => {
      const cache = await readCache();
      cache[lessonId] = notes;
      await kvStore.set(CACHE_KEY, JSON.stringify(cache));
    });
  },

  /** Forgets the queue and every saved note. Each key is removed on its own. */
  clear(): Promise<void> {
    return serial(async () => {
      await Promise.allSettled([kvStore.remove(QUEUE_KEY), kvStore.remove(CACHE_KEY)]);
    });
  },
};