import { useCallback, useEffect, useRef, useState } from 'react';

import { api, NetworkError, UnauthorizedError } from '@/shared/api';
import { lessonCache } from '@/shared/storage';
import { sortLessonsByStart } from '@/shared/storage/sort-lessons';
import type { Lesson } from '@/shared/types/lesson';

import { createLatestGuard } from './latest-guard';

/** Why the last refresh did not produce fresh data. `null` when it did (or has not finished). */
export type TimetableProblem = 'offline' | 'session' | 'failed' | null;

/**
 * The student's timetable, cache first: the last saved lessons are on screen as soon as the local
 * read finishes, then a fresh `GET /timetable` replaces them. First paint never waits on a request.
 */
export function useTimetable() {
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
    gotFreshData.current = true;
    setLessons(fresh);
    setSyncedAt(new Date());
    setProblem(null);
    // Saving is best effort: a storage failure must not hide a timetable we already have.
    lessonCache.replaceLessons(fresh).catch(() => undefined);
  }, []);

  const onFailed = useCallback((error: unknown) => {
    if (error instanceof NetworkError) setProblem('offline');
    else if (error instanceof UnauthorizedError) setProblem('session');
    else setProblem('failed');
  }, []);

  /**
   * Fetches fresh lessons and reconciles them over what is on screen. Only the newest request may
   * touch state, so a slow older response cannot overwrite a newer one. Anything that goes wrong
   * while applying a response is reported as a failed refresh instead of an unhandled rejection.
   */
  const load = useCallback(() => {
    const isNewest = requests.next();
    const current = () => mounted.current && isNewest();
    return api
      .getTimetable()
      .then((lessonsFromServer) => {
        if (current()) onFresh(lessonsFromServer);
      })
      .catch((error: unknown) => {
        if (current()) onFailed(error);
      })
      .finally(() => {
        if (current()) setRefreshing(false);
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

    return () => {
      mounted.current = false;
    };
  }, [load]);

  return { lessons, cacheRead, refreshing, problem, syncedAt, refresh };
}
