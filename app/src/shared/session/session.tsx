import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { onUnauthorized } from '@/shared/api';
import { getToken } from '@/shared/storage/token-store';

interface Session {
  /** `null` until the stored token has been looked up. */
  signedIn: boolean | null;
  /** Call once a token has been stored. */
  markSignedIn: () => void;
}

const SessionContext = createContext<Session | null>(null);

/** Whether this device holds a bearer token. A 401 anywhere in the app signs the student out. */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [signedIn, setSignedIn] = useState<boolean | null>(null);

  useEffect(() => {
    let cancelled = false;
    getToken().then(
      (token) => !cancelled && setSignedIn(token !== null),
      () => !cancelled && setSignedIn(false),
    );
    const unsubscribe = onUnauthorized(() => setSignedIn(false));
    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, []);

  const value = useMemo(() => ({ signedIn, markSignedIn: () => setSignedIn(true) }), [signedIn]);
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) throw new Error('useSession must be used inside SessionProvider');
  return session;
}
