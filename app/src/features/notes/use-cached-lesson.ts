import { useEffect, useState } from 'react';

import { lessonCache } from '@/shared/storage';
import type { Lesson } from '@/shared/types/lesson';

/**
 * One lesson looked up in the offline cache by id, so its details show even with no network.
 * `undefined` while the cache is being read, `null` if there is no such lesson.
 */
export function useCachedLesson(lessonId: string): Lesson | null | undefined {
  const [lesson, setLesson] = useState<Lesson | null | undefined>(undefined);

  useEffect(() => {
    let active = true;
    lessonCache
      .readLessons()
      .then((all) => {
        if (active) setLesson(all.find((l) => l.id === lessonId) ?? null);
      })
      .catch(() => {
        if (active) setLesson(null);
      });
    return () => {
      active = false;
    };
  }, [lessonId]);

  return lesson;
}
