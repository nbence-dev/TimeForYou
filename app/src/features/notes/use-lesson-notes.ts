import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';

import { api, type Note } from '@/shared/api';

import { loadErrorMessage } from './note-rules';

/**
 * A lesson's notes, fetched from the server every time the screen comes into focus. That is what
 * makes a note written on one device show up on another when its app is next opened, and what
 * refreshes the list after the editor closes. Notes are online only for now (#73): there is no
 * local copy to fall back on, so a failed load says so instead of showing stale text.
 */
export function useLessonNotes(lessonId: string) {
  const [notes, setNotes] = useState<Note[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const mounted = useRef(true);
  const latest = useRef(0);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  /** State is only set once the request settles; a newer request makes an older one a no-op. */
  const reload = useCallback(() => {
    const request = ++latest.current;
    const current = () => mounted.current && request === latest.current;
    return api.listNotes(lessonId).then(
      (fresh) => {
        if (!current()) return;
        setNotes(fresh);
        setError(null);
      },
      (failure: unknown) => {
        if (current()) setError(loadErrorMessage(failure));
      },
    );
  }, [lessonId]);

  useFocusEffect(
    useCallback(() => {
      void reload();
    }, [reload]),
  );

  return { notes, error, reload };
}
