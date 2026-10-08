import * as SQLite from "expo-sqlite";

import type { KvStore } from "./kv-store.types";

/** The native backend: SQLite. The web build gets kv-store.web.ts (localStorage) instead. */

const DATABASE_NAME = "timeforyou-kv.db";

let database: Promise<SQLite.SQLiteDatabase> | null = null;

function openDatabase(): Promise<SQLite.SQLiteDatabase> {
  database ??= (async () => {
    const db = await SQLite.openDatabaseAsync(DATABASE_NAME);
    await db.execAsync(`
      CREATE TABLE IF NOT EXISTS kv (
        key TEXT PRIMARY KEY NOT NULL,
        value TEXT NOT NULL
      );
    `);
    return db;
  })().catch((error: unknown) => {
    database = null; // don't cache a failed open: the next call gets a fresh attempt
    throw error;
  });
  return database;
}

export const kvStore: KvStore = {
  async get(key) {
    const db = await openDatabase();
    const row = await db.getFirstAsync<{ value: string }>("SELECT value FROM kv WHERE key = ?", key);
    return row?.value ?? null;
  },

  async set(key, value) {
    const db = await openDatabase();
    await db.runAsync("INSERT OR REPLACE INTO kv (key, value) VALUES (?, ?)", key, value);
  },

  async remove(key) {
    const db = await openDatabase();
    await db.runAsync("DELETE FROM kv WHERE key = ?", key);
  },
};
