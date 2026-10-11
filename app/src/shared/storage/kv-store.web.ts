import type { KvStore } from "./kv-store.types";

/**
 * The web backend: localStorage, which survives a browser restart. Native builds get kv-store.ts
 * (SQLite) instead. Nothing touches `localStorage` at import time (the static export renders in
 * Node, where it does not exist), and a blocked or full store is treated as "nothing stored".
 */

const PREFIX = "timeforyou.kv.";

export const kvStore: KvStore = {
  async get(key) {
    try {
      return localStorage.getItem(PREFIX + key);
    } catch {
      return null;
    }
  },

  async set(key, value) {
    try {
      localStorage.setItem(PREFIX + key, value);
    } catch {
      // Private mode or a full store: the value just will not survive a restart.
    }
  },

  async remove(key) {
    try {
      localStorage.removeItem(PREFIX + key);
    } catch {
      // see above
    }
  },
};
