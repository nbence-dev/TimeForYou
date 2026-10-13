import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';

import { loadLessonNotes, onNotesQueueChanged, type ShownNote } from '@/shared/sync/notes-outbox';

import { loadErrorMessage } from './note-rules';

/**
 * A lesson's notes, refreshed every time the screen comes into focus and whenever a note change is
 * queued or sent (#124). Offline it shows the notes last fetched on this device, with changes made
 * offline on top, marked "Waiting to sync".
 */
export function useLessonNotes(lessonId: string) {
  const [notes, setNotes] = useState<ShownNote[] | null>(null);
  const [offline, setOffline] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const mounted = useRef(true);
  const latest = useRef(0);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  /** State is only set once the load settles; a newer load makes an older one a no-op. */
  const reload = useCallback(() => {
    const request = ++latest.current;
    const current = () => mounted.current && request === latest.current;
    return loadLessonNotes(lessonId).then(
      (loaded) => {
        if (!current()) return;
        setNotes(loaded.notes);
        setOffline(loaded.offline);
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

  // A note saved offline shows at once; a sent one loses its "Waiting to sync" mark.
  useEffect(() => onNotesQueueChanged(() => void reload()), [reload]);

  return { notes, offline, error, reload };
}