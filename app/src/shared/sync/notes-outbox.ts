import { useEffect } from 'react';
import { AppState } from 'react-native';

import { api, ApiError, NetworkError, onReachedServer, UnauthorizedError, type Note } from '@/shared/api';
import { notesStore, sameChange, type QueuedNoteChange } from '@/shared/storage';
import { announceSynced } from '@/shared/sync/synced';

/**
 * Notes that work offline (#124). A change is sent straight away when it can be; when the device is
 * offline it is kept on the device, shown as "Waiting to sync", and sent in order once the app is
 * back online: on open, on return to the foreground, and after any request reaches the server
 * (which includes pull-to-refresh). The app picks each new note's id, so a retried create can't
 * save it twice. Two devices editing the same note: last write wins.
 */

/** A note change as a screen asks for it. A new note gets its id here. */
export type NoteChange =
  | { kind: 'create'; lessonId: string; body: string }
  | { kind: 'update'; id: string; lessonId: string; body: string }
  | { kind: 'delete'; id: string; lessonId: string };

/** A note as the screens show it: `pending` while a change to it is waiting to sync. */
export type ShownNote = Note & { pending?: boolean };

/** A random UUID for a new note. */
export function newNoteId(): string {
  const crypto = (globalThis as { crypto?: { randomUUID?: () => string } }).crypto;
  if (typeof crypto?.randomUUID === 'function') return crypto.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (ch) => {
    const r = Math.floor(Math.random() * 16);
    return (ch === 'x' ? r : (r % 4) + 8).toString(16);
  });
}

const listeners = new Set<() => void>();

/** Calls `listener` whenever the queue changes or a change was sent. Returns the unsubscribe. */
export function onNotesQueueChanged(listener: () => void): () => void {
  listeners.add(listener);
  return () => void listeners.delete(listener);
}

function notify(): void {
  listeners.forEach((listener) => listener());
}

async function send(change: QueuedNoteChange): Promise<void> {
  if (change.kind === 'create') await api.createNote(change.lessonId, change.body, change.id);
  else if (change.kind === 'update') await api.updateNote(change.id, change.body);
  else await api.deleteNote(change.id);
}

/** A change the server refused for good (e.g. 404: the lesson or note is gone). Retrying won't help. */
function isPermanentFailure(error: unknown): boolean {
  return error instanceof ApiError && !(error instanceof UnauthorizedError) && error.status >= 400 && error.status < 500;
}

let flushing: Promise<void> | null = null;
/** True while the first queued change is on its way, so nothing is folded into it. */
let headInFlight = false;

/** Sends the waiting changes in order. Safe to call any time: only one run happens at once. */
export function flushNoteQueue(): Promise<void> {
  flushing ??= runFlush().finally(() => {
    flushing = null;
  });
  return flushing;
}

async function runFlush(): Promise<void> {
  let sentAny = false;
  let last: QueuedNoteChange | null = null;
  try {
    for (;;) {
      const [next] = await notesStore.readQueue();
      // Nothing left, or the last one came back (storage couldn't remove it): stop rather than loop.
      if (!next || (last && sameChange(last, next))) break;
      last = next;

      headInFlight = true;
      try {
        await send(next);
      } catch (error) {
        // Offline, signed out (the queue was just wiped) or a server hiccup: keep it for next time.
        if (!isPermanentFailure(error)) break;
      } finally {
        headInFlight = false;
      }
      await notesStore.remove(next);
      sentAny = true;
    }
  } finally {
    if (sentAny) {
      notify();
      announceSynced(); // the timetable's note icons come from the server, so refetch them
    }
  }
}

/**
 * Saves a note change. Resolves to "sent" when the server has it, or "queued" when it is kept on
 * the device to send later. Rejects only when the server refuses it (e.g. an empty note, or a 404),
 * so the screen can say why.
 */
export async function saveNoteChange(change: NoteChange): Promise<'sent' | 'queued'> {
  const at = new Date().toISOString();
  const queued: QueuedNoteChange =
    change.kind === 'create' ? { ...change, id: newNoteId(), at } : { ...change, at };

  // Changes already waiting go first, so this one waits behind them to keep the order.
  if ((await notesStore.readQueue()).length > 0) {
    await notesStore.enqueue(queued, headInFlight);
    notify();
    void flushNoteQueue();
    return 'queued';
  }

  try {
    await send(queued);
    return 'sent';
  } catch (error) {
    if (!(error instanceof NetworkError)) throw error;
    await notesStore.enqueue(queued);
    notify();
    return 'queued';
  }
}

/** The server's notes with the waiting changes for this lesson applied on top, oldest first. */
export function applyQueued(notes: readonly Note[], queued: readonly QueuedNoteChange[], lessonId: string): ShownNote[] {
  const byId = new Map<string, ShownNote>(notes.map((n) => [n.id, { ...n }]));
  for (const change of queued) {
    if (change.lessonId !== lessonId) continue;
    if (change.kind === 'delete') {
      byId.delete(change.id);
    } else if (change.kind === 'create') {
      byId.set(change.id, {
        id: change.id,
        lesson_id: lessonId,
        body: change.body,
        created_at: change.at,
        updated_at: change.at,
        pending: true,
      });
    } else {
      const note = byId.get(change.id);
      if (note) byId.set(change.id, { ...note, body: change.body, updated_at: change.at, pending: true });
    }
  }
  return [...byId.values()].sort((a, b) => Date.parse(a.created_at) - Date.parse(b.created_at));
}

/**
 * A lesson's notes for the screens: from the server when it can be reached (and saved for offline),
 * otherwise the saved copy, with waiting changes on top. `offline` says the server wasn't reached.
 * Rejects when there is nothing at all to show.
 */
export async function loadLessonNotes(lessonId: string): Promise<{ notes: ShownNote[]; offline: boolean }> {
  let fromServer: Note[] | null;
  let offline = false;
  try {
    fromServer = await api.listNotes(lessonId);
    void notesStore.writeLessonNotes(lessonId, fromServer).catch(() => undefined);
  } catch (error) {
    if (!(error instanceof NetworkError)) throw error;
    offline = true;
    fromServer = await notesStore.readLessonNotes(lessonId);
  }
  const queued = await notesStore.readQueue();
  if (fromServer === null && !queued.some((c) => c.lessonId === lessonId)) throw new NetworkError();
  return { notes: applyQueued(fromServer ?? [], queued, lessonId), offline };
}

/**
 * Sends waiting note changes when the app opens, when it comes back to the foreground, when the
 * browser comes back online, and whenever a request reaches the server. Mount once, while signed in.
 */
export function NotesOutboxSync(): null {
  useEffect(() => {
    void flushNoteQueue();
    const stopReached = onReachedServer(() => void flushNoteQueue());
    const appState = AppState.addEventListener('change', (state) => {
      if (state === 'active') void flushNoteQueue();
    });
    const onOnline = () => void flushNoteQueue();
    const canListen = typeof window !== 'undefined' && typeof window.addEventListener === 'function';
    if (canListen) window.addEventListener('online', onOnline);
    return () => {
      stopReached();
      appState.remove();
      if (canListen) window.removeEventListener('online', onOnline);
    };
  }, []);
  return null;
}