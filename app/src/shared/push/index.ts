// Native build: Android FCM registration is a separate ticket (#70). Until then, no push here.
import type { PushState } from './types';

export type { PushState } from './types';

export async function syncPush(): Promise<PushState> {
  return 'unsupported';
}

export async function enablePush(): Promise<PushState> {
  return 'unsupported';
}

export function onPushReceived(_listener: () => void): () => void {
  return () => {};
}
