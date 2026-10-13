import * as SQLite from "expo-sqlite";

import type { Lesson, LessonMode, LessonStatus } from "@/shared/types/lesson";

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
  group_name: string | null;
  mode: LessonMode | null;
  has_note: number;
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
    `);
    // The cache is disposable (the next sync refills it), so a schema bump drops the table
    // rather than migrating it. user_version 3 added group_name, mode and has_note (#49).
    const version = await db.getFirstAsync<{ user_version: number }>("PRAGMA user_version");
    if ((version?.user_version ?? 0) < 3) {
      await db.execAsync("DROP TABLE IF EXISTS lessons; PRAGMA user_version = 3;");
    }
    await db.execAsync(`
      CREATE TABLE IF NOT EXISTS lessons (
        id TEXT PRIMARY KEY NOT NULL,
        untis_lesson_id TEXT NOT NULL,
        subject TEXT NOT NULL,
        lecturer TEXT,
        room TEXT,
        group_name TEXT,
        mode TEXT,
        has_note INTEGER NOT NULL DEFAULT 0,
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
      "SELECT id, untis_lesson_id, subject, lecturer, room, group_name, mode, has_note, start_ts, end_ts, status FROM lessons",
    );
    return sortLessonsByStart(
      rows.map(({ group_name, has_note, ...rest }): Lesson => ({
        ...rest,
        group: group_name,
        has_note: has_note === 1,
      })),
    );
  },

  async replaceLessons(lessons: readonly Lesson[]) {
    const db = await openDatabase();
    // Exclusive: the transaction runs on its own connection, so reads on the main one rely on WAL.
    await db.withExclusiveTransactionAsync(async (txn) => {
      await txn.runAsync("DELETE FROM lessons");
      for (const lesson of lessons) {
        await txn.runAsync(
          `INSERT OR REPLACE INTO lessons
             (id, untis_lesson_id, subject, lecturer, room, group_name, mode, has_note, start_ts, end_ts, status)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
          lesson.id,
          lesson.untis_lesson_id,
          lesson.subject,
          lesson.lecturer ?? null,
          lesson.room,
          lesson.group ?? null,
          lesson.mode ?? null,
          lesson.has_note ? 1 : 0,
          lesson.start_ts,
          lesson.end_ts,
          lesson.status,
        );
      }
    });
  },
};
