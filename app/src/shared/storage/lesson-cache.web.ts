import type { Lesson } from "@/shared/types/lesson";

import type { LessonCache } from "./lesson-cache.types";
import { sortLessonsByStart } from "./sort-lessons";

/**
 * The web/PWA backend: IndexedDB, with no extra dependency. Native builds get lesson-cache.ts
 * (SQLite) instead — Metro picks the file by platform.
 *
 * Nothing here touches `indexedDB` at import time: the static web export renders in Node, where
 * it does not exist. The database is opened on first use.
 */

const DATABASE_NAME = "timeforyou-cache";
const DATABASE_VERSION = 1;
const LESSONS_STORE = "lessons";

let database: Promise<IDBDatabase> | null = null;

function openDatabase(): Promise<IDBDatabase> {
  database ??= new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open(DATABASE_NAME, DATABASE_VERSION);
    request.onupgradeneeded = () => {
      request.result.createObjectStore(LESSONS_STORE, { keyPath: "id" });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  }).catch((error: unknown) => {
    database = null; // don't cache a failed open: the next call gets a fresh attempt
    throw error;
  });
  return database;
}

function promisify<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

/** Settles when the whole transaction has committed (or rolled back), not just the last request. */
function committed(transaction: IDBTransaction): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  });
}

export const lessonCache: LessonCache = {
  async readLessons() {
    const db = await openDatabase();
    const lessons = await promisify<Lesson[]>(
      db.transaction(LESSONS_STORE, "readonly").objectStore(LESSONS_STORE).getAll(),
    );
    return sortLessonsByStart(lessons);
  },

  async replaceLessons(lessons: readonly Lesson[]) {
    const db = await openDatabase();
    // One readwrite transaction: IndexedDB commits clear + puts together or not at all.
    const transaction = db.transaction(LESSONS_STORE, "readwrite");
    const done = committed(transaction);
    const store = transaction.objectStore(LESSONS_STORE);
    try {
      store.clear();
      for (const lesson of lessons) {
        store.put({ ...lesson, lecturer: lesson.lecturer ?? null });
      }
    } catch (error) {
      // put() throws synchronously for a bad lesson (e.g. no `id`), and IndexedDB does not roll back
      // on a caller's exception: without this the clear() and earlier puts would still commit and
      // leave a truncated timetable where the previous one used to be.
      transaction.abort();
      await done.catch(() => undefined);
      throw error;
    }
    await done;
  },
};
