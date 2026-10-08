import { Platform } from 'react-native';

// iOS only delivers Web Push to a site launched from the Home Screen (#3, iOS PWA push caveat).
// These checks decide whether to guide the student there (#67). Web-only: native builds and the
// static export (which renders in Node, with no `window`) always answer "no".

function hasBrowser(): boolean {
  return Platform.OS === 'web' && typeof window !== 'undefined' && typeof navigator !== 'undefined';
}

/** iPhone, iPod or iPad. iPadOS reports itself as a Mac, so a touch-screen "Mac" counts too. */
export function isIos(): boolean {
  if (!hasBrowser()) return false;
  const ua = navigator.userAgent;
  return /iPhone|iPad|iPod/.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
}

/** Launched from the Home Screen rather than a browser tab. */
export function isStandalone(): boolean {
  if (!hasBrowser()) return false;
  const iosStandalone = (navigator as Navigator & { standalone?: boolean }).standalone === true;
  return iosStandalone || window.matchMedia?.('(display-mode: standalone)').matches === true;
}

/** True on iOS in a browser tab: the student can use the timetable but will get no notifications. */
export function needsHomeScreenInstall(): boolean {
  return isIos() && !isStandalone();
}