/* FCM Web service worker. Firebase config arrives on the registration URL's query string
   (see src/shared/push/index.web.ts), because a worker cannot read build-time env vars. */
// Keep this version in step with the `firebase` dependency in app/package.json: the page and the
// worker should run the same SDK.
importScripts('https://www.gstatic.com/firebasejs/12.19.0/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/12.19.0/firebase-messaging-compat.js');

// Take over open pages straight away, so a changed worker applies without a second reload.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

// Tapping a push opens the Changes screen (#71 fills it in). With a tab open we focus it and ask the
// app to route itself (no openWindow, which Chrome refuses outside a fresh click); with none, we open one.
// This must be registered BEFORE firebase.messaging(): the SDK's own notificationclick handler would
// otherwise run first, and it swallows the event. We stop propagation so it does not also try to open a window.
// iOS Home Screen apps ignore the URL given to openWindow() (WebKit bug 263687), so the worker also
// leaves a short-lived note in the Cache API (readable from the page, unlike localStorage). The page
// looks for it when it opens or returns to the foreground and routes to Changes itself.
// Keep the cache name and key in step with src/shared/push/notification-intent.ts.
async function leaveOpenChangesNote() {
  const cache = await self.caches.open('tfy-notification-intent');
  await cache.put('/open-changes', new Response(String(Date.now())));
}

self.addEventListener('notificationclick', (event) => {
  event.stopImmediatePropagation();
  event.notification.close();
  event.waitUntil(
    (async () => {
      await leaveOpenChangesNote().catch(() => {});
      const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
      const w = windows[0];
      if (w) {
        await w.focus().catch(() => {});
        w.postMessage({ type: 'open-changes' });
        return;
      }
      await self.clients.openWindow('/changes');
    })().catch((e) => console.warn('Could not open the app from a notification', e)),
  );
});

const config = Object.fromEntries(new URL(self.location.href).searchParams);
firebase.initializeApp(config);
// Initialising messaging makes the SDK display `notification` payloads that arrive in the background.
firebase.messaging();
