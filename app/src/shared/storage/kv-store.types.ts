/**
 * A tiny persistent key-value store for small, non-secret values that must survive a restart
 * (#72). Not for credentials: those go through `token-store`. SecureStore cannot hold a list of
 * changes (it caps a value at about 2 KB), so native uses SQLite and web uses localStorage.
 */
export interface KvStore {
  /** The stored value, or `null` if there is none. */
  get(key: string): Promise<string | null>;
  set(key: string, value: string): Promise<void>;
  remove(key: string): Promise<void>;
}
