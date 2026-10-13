import { useCallback, useEffect, useRef, useState } from 'react';

import { onUnauthorized } from '@/shared/api';
import { changesStore } from '@/shared/storage';

import { withArchived } from './change-archive';

/**
 * The ids the student has archived (#138), kept on the device in `changesStore`. `ready` is false
 * until the saved ids have been read, so "Mark all as read" cannot run before they are known.
 */
export function useArchive() {
  const [archived, setArchived] = useState<ReadonlySet<string>>(() => new Set());
  const [ready, setReady] = useState(false);

  const mounted = useRef(true);
  // The latest set, for `archiveAll` to build on without waiting for a render.
  const archivedRef = useRef<ReadonlySet<string>>(archived);
  // Set the moment a 401 or an unlink signs the student out, before the saved state is wiped, so a
  // late write cannot bring the previous student's archive back.
  const signedOut = useRef(false);

  useEffect(() => {
    mounted.current = true;
    signedOut.current = false;
    const stopListening = onUnauthorized(() => {
      signedOut.current = true;
    });

    changesStore
      .readArchivedIds()
      .then((ids) => {
        if (!mounted.current || signedOut.current) return;
        archivedRef.current = new Set(ids);
        setArchived(archivedRef.current);
      })
      .finally(() => {
        if (mounted.current) setReady(true);
      });

    return () => {
      mounted.current = false;
      stopListening();
    };
  }, []);

  /** Archives every change given and saves it. */
  const archiveAll = useCallback((changes: readonly { id: string }[]) => {
    if (signedOut.current) return;
    archivedRef.current = withArchived(archivedRef.current, changes);
    setArchived(archivedRef.current);
    void changesStore.writeArchivedIds([...archivedRef.current]).catch(() => undefined);
  }, []);

  return { archived, ready, archiveAll };
}
