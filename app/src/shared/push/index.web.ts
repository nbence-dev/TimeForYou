// iOS PWA push: FCM Web. Requires the PWA to be on the Home Screen and the permission prompt to
// come from a tap. Firebase manages the VAPID layer itself, so no key is configured here.
import { initializeApp, getApps } from 'firebase/app';
import { getMessaging, getToken, isSupported, onMessage } from 'firebase/messaging';

import { api } from '@/shared/api';

import type { PushState } from './types';

export type { PushState } from './types';

const config = {
  apiKey: process.env.EXPO_PUBLIC_FIREBASE_API_KEY,
  appId: process.env.EXPO_PUBLIC_FIREBASE_APP_ID,
  messagingSenderId: process.env.EXPO_PUBLIC_FIREBASE_SENDER_ID,
  projectId: process.env.EXPO_PUBLIC_FIREBASE_PROJECT_ID,
};
const configured = Object.values(config).every((v) => !!v && !v.startsWith('CHANGEME'));

/** The service worker cannot read env vars, so the config rides along on its URL. */
const SW_URL = `/firebase-messaging-sw.js?${new URLSearchParams(config as Record<string, string>)}`;

let foregroundListening = false;
const pushListeners = new Set<() => void>();

/** Calls `listener` whenever a push arrives while the app is open. Returns the unsubscribe. */
export function onPushReceived(listener: () => void): () => void {
  pushListeners.add(listener);
  return () => void pushListeners.delete(listener);
}

/**
 * Whether push works here, worked out once by `syncPush` on launch. `enablePush` reads this instead
 * of asking again, because asking is async (Firebase opens a test IndexedDB) and anything awaited
 * between the tap and `Notification.requestPermission()` can make iOS ignore the prompt.
 */
let supportedAtLaunch = false;

async function supported(): Promise<boolean> {
  if (!configured || typeof Notification === 'undefined' || !('serviceWorker' in navigator)) {
    return false;
  }
  return isSupported().catch(() => false);
}

/** Gets the FCM Web token (a new one if it changed) and stores it against this user's device. */
async function register(): Promise<PushState> {
  try {
    const app = getApps()[0] ?? initializeApp(config);
    const messaging = getMessaging(app);
    const serviceWorkerRegistration = await navigator.serviceWorker.register(SW_URL);
    await navigator.serviceWorker.ready;

    const token = await getToken(messaging, { serviceWorkerRegistration });
    if (!token) return 'failed';
    await api.registerDevice({ platform: 'web_push', push_token: token });

    // A push that arrives while the app is open is not shown by the browser; show it ourselves.
    if (!foregroundListening) {
      foregroundListening = true;
      onMessage(messaging, (payload) => {
        pushListeners.forEach((listener) => listener());
        const { title, body } = payload.notification ?? {};
        if (title) {
          void serviceWorkerRegistration.showNotification(title, {
            body,
            data: payload.data,
          });
        }
      });
    }
    return 'registered';
  } catch (error) {
    console.warn('Push registration failed', error);
    return 'failed';
  }
}

/**
 * Call on every launch. Never prompts: if permission is already granted it re-registers (so a
 * changed token reaches the server); otherwise it just reports the state.
 */
export async function syncPush(): Promise<PushState> {
  supportedAtLaunch = await supported();
  if (!supportedAtLaunch) return 'unsupported';
  if (Notification.permission === 'granted') return register();
  return Notification.permission === 'denied' ? 'denied' : 'default';
}

/**
 * Asks for permission, then registers. Must be called from a tap, or iOS ignores the prompt, so
 * nothing is awaited before `requestPermission()`. The prompt strip only shows once `syncPush` has
 * finished, so `supportedAtLaunch` is always set by the time this can be called.
 */
export async function enablePush(): Promise<PushState> {
  if (!supportedAtLaunch) return 'unsupported';
  const permission = await Notification.requestPermission();
  if (permission === 'granted') return register();
  return permission === 'denied' ? 'denied' : 'default';
}
