import { useSyncExternalStore } from 'react';

import type { PushState } from './types';

// One copy of where push stands, shared by everything that shows it. Tab screens stay mounted, so
// two screens each keeping their own copy drift apart: the banner turns notifications on and
// Settings carries on saying "Not set up".
let current: PushState = 'checking';
const listeners = new Set<() => void>();

export function setPushState(next: PushState): void {
  if (next === current) return;
  current = next;
  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => void listeners.delete(listener);
}

/** The latest push state. `checking` until the launch check in `PushPrompt` has finished. */
export function usePushState(): PushState {
  return useSyncExternalStore(subscribe, () => current, () => current);
}
