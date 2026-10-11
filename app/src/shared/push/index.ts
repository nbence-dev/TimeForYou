// Native build: Android FCM via expo-notifications. The server sends plain FCM notification
// messages to the raw FCM token, so the system tray shows them while the app is closed or in the
// background; this file handles permission, the token, and the foreground and tap cases.
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

import { api } from '@/shared/api';

import type { PushState } from './types';

export type { PushState } from './types';

// Without this a push that arrives while the app is open is dropped silently.
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: false,
    shouldSetBadge: false,
  }),
});

let tokenListening = false;

/** Stores the raw FCM token against this user's device, and again whenever Firebase rotates it. */
async function register(): Promise<PushState> {
  try {
    if (!tokenListening) {
      tokenListening = true;
      Notifications.addPushTokenListener((token) => {
        api
          .registerDevice({ platform: 'android_fcm', push_token: String(token.data) })
          .catch((error) => console.warn('Push re-registration failed', error));
      });
    }
    const { data } = await Notifications.getDevicePushTokenAsync();
    await api.registerDevice({ platform: 'android_fcm', push_token: String(data) });
    return 'registered';
  } catch (error) {
    console.warn('Push registration failed', error);
    return 'failed';
  }
}

function fromPermission(status: Notifications.PermissionStatus, canAskAgain: boolean): PushState {
  if (status === Notifications.PermissionStatus.GRANTED) return 'registered';
  return status === Notifications.PermissionStatus.DENIED && !canAskAgain ? 'denied' : 'default';
}

/**
 * Call on every launch. Never prompts: if permission is already granted it re-registers (so a
 * changed token reaches the server); otherwise it just reports the state.
 */
export async function syncPush(): Promise<PushState> {
  if (Platform.OS !== 'android') return 'unsupported';
  try {
    // Android 13+ only shows the permission prompt once a notification channel exists.
    await Notifications.setNotificationChannelAsync('default', {
      name: 'Timetable changes',
      importance: Notifications.AndroidImportance.DEFAULT,
    });
    const { status, canAskAgain } = await Notifications.getPermissionsAsync();
    const state = fromPermission(status, canAskAgain);
    return state === 'registered' ? register() : state;
  } catch (error) {
    console.warn('Push setup failed', error);
    return 'failed';
  }
}

/** Asks for permission, then registers. A refusal is a handled state, not an error. */
export async function enablePush(): Promise<PushState> {
  if (Platform.OS !== 'android') return 'unsupported';
  const { status, canAskAgain } = await Notifications.requestPermissionsAsync();
  const state = fromPermission(status, canAskAgain);
  // A first refusal leaves 'default' (Android will prompt again); 'denied' only once it will not.
  return state === 'registered' ? register() : state;
}

/** Calls `listener` whenever a push arrives while the app is open. Returns the unsubscribe. */
export function onPushReceived(listener: () => void): () => void {
  const subscription = Notifications.addNotificationReceivedListener(() => listener());
  return () => subscription.remove();
}

/**
 * Calls `listener` when a notification is tapped, including the tap that cold-started the app.
 * Returns the unsubscribe.
 */
export function onNotificationTapped(listener: () => void): () => void {
  // The last response outlives the tap, so consume it or a remount would replay it.
  if (Notifications.getLastNotificationResponse()) {
    Notifications.clearLastNotificationResponse();
    listener();
  }
  const subscription = Notifications.addNotificationResponseReceivedListener(() => listener());
  return () => subscription.remove();
}
