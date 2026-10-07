/* FCM Web service worker. Firebase config arrives on the registration URL's query string
   (see src/shared/push/index.web.ts), because a worker cannot read build-time env vars. */
importScripts('https://www.gstatic.com/firebasejs/12.19.0/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/12.19.0/firebase-messaging-compat.js');

const config = Object.fromEntries(new URL(self.location.href).searchParams);
firebase.initializeApp(config);
// Initialising messaging makes the SDK display `notification` payloads that arrive in the background.
firebase.messaging();

// Tapping a push opens the Changes screen (#71 fills it in), reusing an open window if there is one.
self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      for (const w of windows) {
        if ('focus' in w) {
          return w
            .navigate('/changes')
            .then((c) => (c || w).focus())
            .catch(() => self.clients.openWindow('/changes'));
        }
      }
      return self.clients.openWindow('/changes');
    }),
  );
});
