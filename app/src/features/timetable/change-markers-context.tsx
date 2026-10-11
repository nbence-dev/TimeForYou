import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

import { api, onUnauthorized } from '@/shared/api';
import type { ChangeLogEntry } from '@/shared/api/types';
import { onPushReceived } from '@/shared/push';
import { changesStore } from '@/shared/storage';

import {
  initialSeenIds,
  markAllSeen as withAllSeen,
  markLessonSeen as withLessonSeen,
  unseenChanges,
  unseenLessonIds,
} from './change-markers';

type ChangeMarkers = {
  /** Lessons carrying a change the student has not seen yet: these get the orange dot. */
  unseenLessonIds: ReadonlySet<string>;
  /** How many unseen changes there are: the number on the Changes tab. */
  unseenCount: number;
  /** The student opened this lesson: its changes count as seen. */
  markLessonSeen: (lessonId: string) => void;
  /** The student opened the Changes tab: every change counts as seen. */
  markAllSeen: () => void;
  /** Fetch the changes again (after a pull-to-refresh). Never rejects. */
  reload: () => Promise<void>;
};

const NONE: ChangeMarkers = {
  unseenLessonIds: new Set(),
  unseenCount: 0,
  markLessonSeen: () => {},
  markAllSeen: () => {},
  reload: async () => {},
};

const ChangeMarkersContext = createContext<ChangeMarkers>(NONE);

/** The markers for the current screen. Without a provider there are simply none. */
export function useChangeMarkers(): ChangeMarkers {
  return useContext(ChangeMarkersContext);
}

/**
 * Works out which changes the student has not seen (#72) and shares it, so the dot on a lesson and
 * the number on the Changes tab always agree.
 *
 * A change is seen once the student opens its lesson or opens the Changes tab, and not before: one
 * on a day they never looked at stays marked, across restarts, until they do. The first time the
 * app ever runs, whatever history already exists counts as seen, so only later changes mark. The
 * last fetched list and the seen ids are kept on the device, so the markers work offline.
 *
 * The tabs layout calls `markAllSeen` whenever the Changes tab is showing, however it was reached.
 */
export function ChangeMarkersProvider({ children }: { children: ReactNode }) {
  const [changes, setChanges] = useState<ChangeLogEntry[]>([]);
  // `null` until known: nothing is marked before then, so a fresh install stays quiet.
  const [seen, setSeen] = useState<ReadonlySet<string> | null>(null);

  const mounted = useRef(true);
  // The latest of each, for the actions below to build on without waiting for a render.
  const changesRef = useRef<ChangeLogEntry[]>([]);
  const seenRef = useRef<ReadonlySet<string> | null>(null);
  // False if the saved state could not be read at all. That must not look like a first launch (it
  // would count every unseen change as seen), so the markers stay off and nothing is overwritten.
  const storageReadable = useRef(true);
  // Set the moment a 401 signs the student out, before the saved state is wiped. From then on
  // nothing is shown or saved, so a fetch still in flight cannot write the old student's data back.
  const signedOut = useRef(false);
  // The first read of the saved state. A refresh waits for it, so it cannot race it.
  const initialRead = useRef<Promise<void>>(Promise.resolve());
  // Counts fetches started, so a slow older response cannot replace a newer list.
  const latestFetch = useRef(0);

  /** Takes this as the seen set and saves it. */
  const remember = useCallback((next: ReadonlySet<string>) => {
    if (signedOut.current) return;
    seenRef.current = next;
    setSeen(next);
    void changesStore.writeSeenIds([...next]).catch(() => undefined);
  }, []);

  const show = useCallback(
    (list: ChangeLogEntry[], fresh: boolean) => {
      if (signedOut.current) return;
      changesRef.current = list;
      setChanges(list);
      if (!fresh || !storageReadable.current) return;

      // The very first launch: everything that already exists counts as seen.
      if (seenRef.current === null) remember(initialSeenIds(list));
      void changesStore.writeCached(list).catch(() => undefined);
    },
    [remember],
  );

  const markLessonSeen = useCallback(
    (lessonId: string) => {
      const current = seenRef.current;
      if (current === null) return;
      const next = withLessonSeen(current, changesRef.current, lessonId);
      if (next.size !== current.size) remember(next);
    },
    [remember],
  );

  const markAllSeen = useCallback(() => {
    const current = seenRef.current;
    if (current === null) return;
    const next = withAllSeen(current, changesRef.current);
    if (next.size !== current.size) remember(next);
  }, [remember]);

  const reload = useCallback(
    () =>
      initialRead.current.then(() => {
        const mine = ++latestFetch.current;
        return api.getChanges().then(
          (list) => {
            if (mounted.current && mine === latestFetch.current) show(list, true);
          },
          () => {
            // Offline or failing: keep the saved list. The markers still work from it.
          },
        );
      }),
    [show],
  );

  useEffect(() => {
    mounted.current = true;
    signedOut.current = false;
    const stopListening = onUnauthorized(() => {
      signedOut.current = true;
    });

    initialRead.current = Promise.allSettled([changesStore.readSeenIds(), changesStore.readCached()]).then(
      ([savedSeen, savedChanges]) => {
        if (!mounted.current) return;
        if (savedSeen.status === 'rejected') {
          storageReadable.current = false;
        } else if (savedSeen.value !== null) {
          const stored = new Set(savedSeen.value);
          seenRef.current = stored;
          setSeen(stored);
        }
        show(savedChanges.status === 'fulfilled' ? savedChanges.value : [], false);
      },
    );
    void reload();

    // A new change shows up without a pull-to-refresh: when a push arrives while the app is open,
    // and when the app comes back to the foreground.
    const stopPush = onPushReceived(() => void reload());
    const onVisible = () => {
      if (typeof document === 'undefined' || document.visibilityState === 'visible') void reload();
    };
    const canListen = typeof document !== 'undefined' && typeof window !== 'undefined';
    if (canListen) {
      document.addEventListener('visibilitychange', onVisible);
      window.addEventListener('focus', onVisible);
    }

    return () => {
      mounted.current = false;
      stopListening();
      stopPush();
      if (canListen) {
        document.removeEventListener('visibilitychange', onVisible);
        window.removeEventListener('focus', onVisible);
      }
    };
  }, [show, reload]);

  const value = useMemo<ChangeMarkers>(
    () => ({
      unseenLessonIds: unseenLessonIds(changes, seen),
      unseenCount: unseenChanges(changes, seen).length,
      markLessonSeen,
      markAllSeen,
      reload,
    }),
    [changes, seen, markLessonSeen, markAllSeen, reload],
  );

  return <ChangeMarkersContext.Provider value={value}>{children}</ChangeMarkersContext.Provider>;
}
