import { ScrollViewStyleReset } from 'expo-router/html';
import type { PropsWithChildren } from 'react';

/**
 * The HTML shell for the web build. Only used on web, at build time.
 * The apple-/mobile-web-app tags make a Home Screen icon open full-screen (standalone), which is
 * what iOS needs before it will deliver Web Push, and what install.ts checks for (#67).
 */
export default function Root({ children }: PropsWithChildren) {
  return (
    <html lang="en">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-title" content="TimeForYou" />
        <ScrollViewStyleReset />
      </head>
      <body>{children}</body>
    </html>
  );
}