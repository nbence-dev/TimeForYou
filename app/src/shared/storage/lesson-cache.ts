import * as SQLite from "expo-sqlite";

import type { Lesson, LessonStatus } from "@/shared/types/lesson";

import type { LessonCache } from "./lesson-cache.types";
import { sortLessonsByStart } from "./sort-lessons";

/**
 * The native (Android) backend: SQLite. The web build gets lesson-cache.web.ts instead — Metro
 * picks the file by platform, so callers import `lessonCache` from "@/shared/storage" either way.
 */

const DATABASE_NAME = "timeforyou-cache.db";

type LessonRow = {
  id: string;
  untis_lesson_id: string;
  subject: string;
  lecturer: string | null;
  room: string | null;
  start_ts: string;
  end_ts: string;
  status: LessonStatus;
};

let database: Promise<SQLite.SQLiteDatabase> | null = null;

function openDatabase(): Promise<SQLite.SQLiteDatabase> {
  database ??= (async () => {
    const db = await SQLite.openDatabaseAsync(DATABASE_NAME);
    // WAL lets readLessons() run while replaceLessons() holds its write transaction on a second
    // connection; without it that read can fail with "database is locked".
    await db.execAsync(`
      PRAGMA journal_mode = WAL;
      CREATE TABLE IF NOT EXISTS lessons (
        id TEXT PRIMARY KEY NOT NULL,
        untis_lesson_id TEXT NOT NULL,
        subject TEXT NOT NULL,
        lecturer TEXT,
        room TEXT,
        start_ts TEXT NOT NULL,
        end_ts TEXT NOT NULL,
        status TEXT NOT NULL
      );
    `);
    return db;
  })().catch((error: unknown) => {
    database = null; // don't cache a failed open: the next call gets a fresh attempt
    throw error;
  });
  return database;
}

export const lessonCache: LessonCache = {
  async readLessons() {
    const db = await openDatabase();
    const rows = await db.getAllAsync<LessonRow>(
      "SELECT id, untis_lesson_id, subject, lecturer, room, start_ts, end_ts, status FROM lessons",
    );
    return sortLessonsByStart(rows);
  },

  async replaceLessons(lessons: readonly Lesson[]) {
    const db = await openDatabase();
    // Exclusive: the transaction runs on its own connection, so reads on the main one rely on WAL.
    await db.withExclusiveTransactionAsync(async (txn) => {
      await txn.runAsync("DELETE FROM lessons");
      for (const lesson of lessons) {
        await txn.runAsync(
          `INSERT OR REPLACE INTO lessons
             (id, untis_lesson_id, subject, lecturer, room, start_ts, end_ts, status)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
          lesson.id,
          lesson.untis_lesson_id,
          lesson.subject,
          lesson.lecturer ?? null,
          lesson.room,
          lesson.start_ts,
          lesson.end_ts,
          lesson.status,
        );
      }
    });
  },
};
