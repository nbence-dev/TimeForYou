import { useCallback, useEffect, useRef, useState } from 'react';

import { api, NetworkError, UnauthorizedError } from '@/shared/api';
import { onPushReceived } from '@/shared/push';
import { lessonCache } from '@/shared/storage';
import { sortLessonsByStart } from '@/shared/storage/sort-lessons';
import { onSynced } from '@/shared/sync/synced';
import type { Lesson } from '@/shared/types/lesson';

import { createLatestGuard } from './latest-guard';

/** Why the last refresh did not produce fresh data. `null` when it did (or has not finished). */
export type TimetableProblem = 'offline' | 'session' | 'failed' | null;


/**
 * How often a screen asks again while the student has no lessons yet: their first poll is still
 * running after linking (#58). Shared by Today and Timetable (#142), so both wait the same time.
 */
export const RETRY_WHILE_EMPTY_MS = 5_000;

export type UseTimetableOptions = {
  /**
   * While there are no lessons at all, fetch again this often (ms) until some arrive. A student who
   * opens the app right after linking can get an empty timetable while their first poll is still
   * running (#58); this lets the empty state replace itself without a restart (#60).
   */
  retryWhileEmptyMs?: number;
};

/**
 * The student's timetable, cache first: the last saved lessons are on screen as soon as the local
 * read finishes, then a fresh `GET /timetable` replaces them. First paint never waits on a request.
 */
export function useTimetable({ retryWhileEmptyMs }: UseTimetableOptions = {}) {
  const [lessons, setLessons] = useState<Lesson[]>([]);
  const [cacheRead, setCacheRead] = useState(false);
  const [refreshing, setRefreshing] = useState(true);
  const [problem, setProblem] = useState<TimetableProblem>(null);
  const [syncedAt, setSyncedAt] = useState<Date | null>(null);

  const mounted = useRef(true);
  const gotFreshData = useRef(false);
  const [requests] = useState(createLatestGuard);

  // State is only ever set from the promise callbacks below, after the request settles.
  const onFresh = useCallback((lessonsFromServer: Lesson[]) => {
    const fresh = sortLessonsByStart(lessonsFromServer);
    setLessons(fresh);
    // Tells the cache read, which may still be in flight, not to replace these lessons.
    gotFreshData.current = true;
    setSyncedAt(new Date());
    setProblem(null);
    // Saving is best effort: a storage failure, even a synchronous one, must not turn a
    // timetable we already have into a failed refresh.
    Promise.resolve()
      .then(() => lessonCache.replaceLessons(fresh))
      .catch(() => undefined);
  }, []);

  const onFailed = useCallback((error: unknown) => {
    if (error instanceof NetworkError) setProblem('offline');
    else if (error instanceof UnauthorizedError) setProblem('session');
    else setProblem('failed');
  }, []);

  /**
   * Fetches fresh lessons and reconciles them over what is on screen. A slow older response cannot
   * overwrite a newer one, but it still applies if the newer request failed. Only the newest
   * request reports a problem. Anything that goes wrong while applying a response is reported as
   * a failed refresh instead of an unhandled rejection.
   */
  const load = useCallback(() => {
    const request = requests.next();
    const newest = () => mounted.current && request.isNewest();
    return api
      .getTimetable()
      .then((lessonsFromServer) => {
        if (!mounted.current || request.isSuperseded()) return;
        onFresh(lessonsFromServer);
        request.markApplied();
      })
      .catch((error: unknown) => {
        if (newest()) onFailed(error);
      })
      .finally(() => {
        if (newest()) setRefreshing(false);
      });
  }, [requests, onFresh, onFailed]);

  /** A refresh the student asked for (pull-down, retry). The first load starts as `refreshing`. */
  const refresh = useCallback(async () => {
    setRefreshing(true);
    await load();
  }, [load]);

  useEffect(() => {
    mounted.current = true;

    lessonCache
      .readLessons()
      .then((cached) => {
        // The network can win the race; never replace fresh lessons with older cached ones.
        if (mounted.current && !gotFreshData.current) setLessons(cached);
      })
      .catch(() => undefined)
      .finally(() => {
        if (mounted.current) setCacheRead(true);
      });

    load();
    // Settings' Sync now fetched fresh data: fetch again so this screen shows it too.
    const stopSynced = onSynced(() => void load());
    // A lesson changed while the app was open or in the background: the same moments the change
    // markers reload on. Tab screens stay mounted, so without these the lessons stay as they were
    // fetched while the badge moves on (#130). A failed background fetch keeps the lessons shown.
    const stopPush = onPushReceived(() => void load());
    const onVisible = () => {
      if (typeof document === 'undefined' || document.visibilityState === 'visible') void load();
    };
    const canListen = typeof document !== 'undefined' && typeof window !== 'undefined';
    if (canListen) {
      document.addEventListener('visibilitychange', onVisible);
      window.addEventListener('focus', onVisible);
    }

    return () => {
      mounted.current = false;
      stopSynced();
      stopPush();
      if (canListen) {
        document.removeEventListener('visibilitychange', onVisible);
        window.removeEventListener('focus', onVisible);
      }
    };
  }, [load]);

  // Empty after a finished fetch: try again shortly. Stops once lessons arrive, and never retries a
  // dead session (that needs the student to link again, not another request).
  useEffect(() => {
    if (!retryWhileEmptyMs || !cacheRead || refreshing || lessons.length > 0 || problem === 'session') return;
    const id = setTimeout(() => {
      void refresh();
    }, retryWhileEmptyMs);
    return () => clearTimeout(id);
  }, [retryWhileEmptyMs, cacheRead, refreshing, lessons.length, problem, refresh]);

  return { lessons, cacheRead, refreshing, problem, syncedAt, refresh };
}