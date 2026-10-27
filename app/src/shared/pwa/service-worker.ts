import { Platform } from 'react-native';

// The one service worker the web app has: it delivers FCM Web pushes and keeps the app shell, so an
// installed PWA can launch with no signal. Push (src/shared/push/index.web.ts) and the offline shell
// both register it here, with the same URL; a different URL would replace the worker. Web-only:
// native builds and the static export (which renders in Node) never register anything.

export const firebaseConfig = {
  apiKey: process.env.EXPO_PUBLIC_FIREBASE_API_KEY,
  appId: process.env.EXPO_PUBLIC_FIREBASE_APP_ID,
  messagingSenderId: process.env.EXPO_PUBLIC_FIREBASE_SENDER_ID,
  projectId: process.env.EXPO_PUBLIC_FIREBASE_PROJECT_ID,
};
export const firebaseConfigured = Object.values(firebaseConfig).every(
  (v) => !!v && !v.startsWith('CHANGEME'),
);

/** The worker cannot read env vars, so the config rides along on its URL. */
const SW_URL = firebaseConfigured
  ? `/firebase-messaging-sw.js?${new URLSearchParams(firebaseConfig as Record<string, string>)}`
  : '/firebase-messaging-sw.js';

let registration: Promise<ServiceWorkerRegistration> | null = null;

/** Registers the worker once per launch and resolves when it is active. */
export function registerServiceWorker(): Promise<ServiceWorkerRegistration> {
  registration ??= navigator.serviceWorker
    .register(SW_URL)
    .then(() => navigator.serviceWorker.ready)
    .catch((error: unknown) => {
      registration = null;
      throw error;
    });
  return registration;
}

/**
 * Call once on launch. Registers the worker even if the student never allows notifications, then
 * hands it everything this page has loaded so far, which it could not see on a first visit.
 * Skipped in development, where Metro's bundles change on every save.
 */
export async function keepAppShellOffline(): Promise<void> {
  if (__DEV__ || Platform.OS !== 'web' || typeof navigator === 'undefined') return;
  if (!('serviceWorker' in navigator)) return;
  try {
    const { active } = await registerServiceWorker();
    const urls = [
      '/',
      location.href,
      ...performance.getEntriesByType('resource').map((entry) => entry.name),
    ].filter((url) => new URL(url, location.origin).origin === location.origin);
    active?.postMessage({ type: 'cache-shell', urls });
  } catch (error) {
    console.warn('Could not keep the app available offline', error);
  }
}
