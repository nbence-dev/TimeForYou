import { useCallback, useEffect, useRef, useState } from 'react';

import { api, NetworkError, UnauthorizedError, type ChangeLogEntry, type Lesson } from '@/shared/api';
import { lessonCache } from '@/shared/storage';

/** Why the last load did not produce fresh changes. `null` when it did (or has not finished). */
export type ChangesProblem = 'offline' | 'session' | 'failed' | null;

/**
 * Recent changes from `GET /changes`, and the lessons they point at. A change only carries a
 * `lesson_id`, so the timetable is fetched alongside it to put a name and a time on each card.
 */
export function useChanges() {
  const [changes, setChanges] = useState<ChangeLogEntry[]>([]);
  const [lessons, setLessons] = useState<Map<string, Lesson>>(() => new Map());
  const [loaded, setLoaded] = useState(false);
  const [refreshing, setRefreshing] = useState(true);
  const [problem, setProblem] = useState<ChangesProblem>(null);
  const [checkedAt, setCheckedAt] = useState<Date | null>(null);

  const mounted = useRef(true);
  // Only the newest request may update the screen, so a slow older one can't overwrite it.
  const latest = useRef(0);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const refresh = useCallback(async () => {
    const request = ++latest.current;
    const current = () => mounted.current && request === latest.current;
    setRefreshing(true);
    try {
      const [freshChanges, timetable] = await Promise.all([
        api.getChanges(),
        // Labels only: if the timetable can't be fetched, the saved one is good enough.
        api.getTimetable().catch(() => lessonCache.readLessons().catch((): Lesson[] => [])),
      ]);
      if (!current()) return;
      setChanges(freshChanges);
      setLessons(new Map(timetable.map((lesson) => [lesson.id, lesson])));
      setCheckedAt(new Date());
      setProblem(null);
    } catch (error) {
      if (!current()) return;
      if (error instanceof NetworkError) setProblem('offline');
      else if (error instanceof UnauthorizedError) setProblem('session');
      else setProblem('failed');
    } finally {
      if (current()) {
        setRefreshing(false);
        setLoaded(true);
      }
    }
  }, []);

  return { changes, lessons, loaded, refreshing, problem, checkedAt, refresh };
}   