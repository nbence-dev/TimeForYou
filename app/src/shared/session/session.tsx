import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { onUnauthorized } from '@/shared/api';
import { needsHomeScreenInstall } from '@/shared/pwa/install';
import { getToken } from '@/shared/storage/token-store';

interface Session {
  /** `null` until the stored token has been looked up. */
  signedIn: boolean | null;
  /** Call once a token has been stored. */
  markSignedIn: () => void;
  /** True while the Add-to-Home-Screen guide should be on screen (#67). */
  showingInstallGuide: boolean;
  /** Reopens the guide, e.g. from the "notifications are off" banner. */
  openInstallGuide: () => void;
  /** "I've added it" or "Skip for now": back to the app. */
  closeInstallGuide: () => void;
}

const SessionContext = createContext<Session | null>(null);

/** Whether this device holds a bearer token. A 401 anywhere in the app signs the student out. */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [signedIn, setSignedIn] = useState<boolean | null>(null);
  const [showingInstallGuide, setShowingInstallGuide] = useState(false);

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

  const markSignedIn = useCallback(() => {
    setSignedIn(true);
    // iOS only delivers Web Push from the Home Screen, so guide the student there right after linking
    // (#67). Everywhere else (Android, desktop, already installed) this stays false.
    setShowingInstallGuide(needsHomeScreenInstall());
  }, []);

  const openInstallGuide = useCallback(() => setShowingInstallGuide(true), []);
  const closeInstallGuide = useCallback(() => setShowingInstallGuide(false), []);

  const value = useMemo(
    () => ({ signedIn, markSignedIn, showingInstallGuide, openInstallGuide, closeInstallGuide }),
    [signedIn, markSignedIn, showingInstallGuide, openInstallGuide, closeInstallGuide],
  );
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) throw new Error('useSession must be used inside SessionProvider');
  return session;
}