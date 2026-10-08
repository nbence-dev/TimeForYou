// The service worker cannot reliably deep-link an iOS Home Screen app (WebKit bug 263687), so on a
// notification tap it leaves a timestamp note in the Cache API. The app looks for it on launch and
// whenever it returns to the foreground. Keep the cache name and key in step with
// public/firebase-messaging-sw.js.
const CACHE_NAME = 'tfy-notification-intent';
const KEY = '/open-changes';
// A note older than this is a stale tap, not one the student is waiting on.
const MAX_AGE_MS = 2 * 60 * 1000;

/** True once if a notification tap is waiting to open Changes. Consumes the note. */
export async function takeOpenChangesNote(): Promise<boolean> {
  if (typeof window === 'undefined' || !('caches' in window)) return false;
  try {
    const cache = await window.caches.open(CACHE_NAME);
    const note = await cache.match(KEY);
    if (!note) return false;
    await cache.delete(KEY);
    const tappedAt = Number(await note.text());
    return Number.isFinite(tappedAt) && Date.now() - tappedAt < MAX_AGE_MS;
  } catch {
    return false;
  }
}
