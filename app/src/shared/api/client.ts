import { lessonCache } from '@/shared/storage';
import { clearToken, getToken, setToken } from '@/shared/storage/token-store';

import { ApiError, NetworkError, UnauthorizedError } from './errors';
import type {
  ChangeLogEntry,
  Device,
  DeviceRegisterRequest,
  Lesson,
  Note,
  QrScanRequest,
  QrScanResponse,
} from './types';

const BASE_URL = (process.env.EXPO_PUBLIC_API_URL ?? '').replace(/\/+$/, '');

type UnauthorizedListener = () => void;
const unauthorizedListeners = new Set<UnauthorizedListener>();

/** Called after a 401 has cleared the token. Returns an unsubscribe function. */
export function onUnauthorized(listener: UnauthorizedListener): () => void {
  unauthorizedListeners.add(listener);
  return () => unauthorizedListeners.delete(listener);
}

/**
 * The saved timetable belongs to whoever was signed in. When that changes hands (a 401 signs the
 * student out, or a new scan links an account) it must not be shown to the next person, so it is
 * emptied. Best effort: if the wipe fails the next successful fetch replaces it anyway.
 */
async function forgetSavedTimetable(): Promise<void> {
  try {
    await lessonCache.replaceLessons([]);
  } catch {
    // see above
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE';
  body?: unknown;
  query?: Record<string, string | undefined>;
  /** false only for endpoints that skip bearer auth (`/qr-scan`). */
  auth?: boolean;
}

async function request<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, auth = true } = opts;

  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(query ?? {})) {
    if (v !== undefined) params.set(k, v);
  }
  const qs = params.toString();
  const url = `${BASE_URL}${path}${qs ? `?${qs}` : ''}`;

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (auth) {
    const token = await getToken();
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  let res: Response;
  try {
    res = await fetch(url, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch (e) {
    throw new NetworkError(e);
  }

  if (!res.ok) {
    const message = await readErrorMessage(res);
    if (res.status === 401 && auth) {
      await clearToken();
      await forgetSavedTimetable();
      unauthorizedListeners.forEach((l) => l());
      throw new UnauthorizedError(message);
    }
    throw new ApiError(res.status, message);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

async function readErrorMessage(res: Response): Promise<string> {
  try {
    const data = (await res.json()) as { message?: unknown };
    if (typeof data.message === 'string') return data.message;
  } catch {
    // body was not JSON; fall through
  }
  return res.statusText || `Request failed (${res.status})`;
}

const noteBase = (lessonId: string) => `/lessons/${encodeURIComponent(lessonId)}/notes`;
const noteUrl = (noteId: string) => `/notes/${encodeURIComponent(noteId)}`;

export const api = {
  /** Exchanges a scanned QR payload for a bearer token and stores it on the device. */
  async scanQr(payload: QrScanRequest): Promise<QrScanResponse> {
    const res = await request<QrScanResponse>('/qr-scan', {
      method: 'POST',
      body: payload,
      auth: false,
    });
    await setToken(res.token);
    // A new account on this device: whatever was saved belonged to the previous one.
    await forgetSavedTimetable();
    return res;
  },

  getTimetable: () => request<Lesson[]>('/timetable'),

  /** `since` is an ISO-8601 timestamp; the API defaults to 30 days ago. */
  getChanges: (since?: string) => request<ChangeLogEntry[]>('/changes', { query: { since } }),

  registerDevice: (payload: DeviceRegisterRequest) =>
    request<Device>('/devices', { method: 'POST', body: payload }),

  listNotes: (lessonId: string) => request<Note[]>(noteBase(lessonId)),

  createNote: (lessonId: string, body: string) =>
    request<Note>(noteBase(lessonId), { method: 'POST', body: { body } }),

  updateNote: (noteId: string, body: string) =>
    request<Note>(noteUrl(noteId), { method: 'PATCH', body: { body } }),

  deleteNote: (noteId: string) => request<void>(noteUrl(noteId), { method: 'DELETE' }),
};
