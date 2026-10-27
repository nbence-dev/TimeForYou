/* FCM Web service worker, which also keeps the app shell for offline launches. Firebase config
   arrives on the registration URL's query string (see src/shared/pwa/service-worker.ts), because a
   worker cannot read build-time env vars. */
// Keep this version in step with the `firebase` dependency in app/package.json: the page and the
// worker should run the same SDK.
importScripts('https://www.gstatic.com/firebasejs/12.19.0/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/12.19.0/firebase-messaging-compat.js');

// The offline shell: the HTML pages and the hashed bundles, fonts and images they load. Lessons,
// notes and the session are not here; the app keeps those in IndexedDB and localStorage.
// Bump the version to drop every cached shell on the next worker update.
const SHELL_CACHE = 'tfy-shell-v1';
// Old releases' bundles pile up under new hashes; past this many entries the oldest go first.
const MAX_SHELL_ENTRIES = 80;
// On a weak signal, wait this long for a fresh page before opening the cached one.
const NAVIGATION_TIMEOUT_MS = 4000;

// Take over open pages straight away, so a changed worker applies without a second reload.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) =>
  event.waitUntil(
    (async () => {
      const names = await self.caches.keys();
      await Promise.all(
        names.filter((n) => n.startsWith('tfy-shell-') && n !== SHELL_CACHE).map((n) => self.caches.delete(n)),
      );
      await self.clients.claim();
    })(),
  ),
);

/** expo export puts a content hash in every bundle, font and image name, so a cached copy never goes stale. */
function isHashedAsset(url) {
  return url.pathname.startsWith('/_expo/static/') || /\.[0-9a-f]{32}\.\w+$/.test(url.pathname);
}

async function putInShell(cache, request, response) {
  if (!response.ok || response.type !== 'basic') return;
  await cache.put(request, response);
  const keys = await cache.keys();
  await Promise.all(keys.slice(0, Math.max(0, keys.length - MAX_SHELL_ENTRIES)).map((k) => cache.delete(k)));
}

/**
 * Fresh page when the network answers in time. The cached one (or the `/` shell) when offline, on a
 * stalled signal, or when the server is down and Cloudflare answers with its own error page.
 */
async function navigation(event) {
  const cache = await self.caches.open(SHELL_CACHE);
  const network = fetch(event.request).then(async (response) => {
    await putInShell(cache, event.request, response.clone());
    return response;
  });
  event.waitUntil(network.catch(() => {}));

  // Dynamic routes (/lesson/123) are served index.html by Caddy too, so the `/` shell is a safe fallback.
  const cached = (await cache.match(event.request, { ignoreSearch: true })) ?? (await cache.match('/'));
  if (!cached) return network;
  const fresh = network.then(
    (response) => (response.ok ? response : cached),
    () => cached,
  );
  const timeout = new Promise((resolve) => setTimeout(() => resolve(cached), NAVIGATION_TIMEOUT_MS));
  return Promise.race([fresh, timeout]);
}

async function hashedAsset(event) {
  const cache = await self.caches.open(SHELL_CACHE);
  const cached = await cache.match(event.request);
  if (cached) return cached;
  const response = await fetch(event.request);
  event.waitUntil(putInShell(cache, event.request, response.clone()));
  return response;
}

// Same-origin GETs only. The API is on its own origin and is never cached here, and anything else
// (the Metro dev server's unhashed bundles included) goes straight to the network.
self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (request.mode === 'navigate') event.respondWith(navigation(event));
  else if (isHashedAsset(url)) event.respondWith(hashedAsset(event));
});

// The page that registered this worker loaded before the worker could see its requests, so it
// sends what it loaded (src/shared/pwa/service-worker.ts) and the first launch is enough to go offline.
self.addEventListener('message', (event) => {
  if (event.data?.type !== 'cache-shell' || !Array.isArray(event.data.urls)) return;
  event.waitUntil(
    (async () => {
      const cache = await self.caches.open(SHELL_CACHE);
      for (const href of event.data.urls) {
        const url = new URL(href, self.location.origin);
        // Pages (no file extension) and hashed assets: the two things the fetch handler serves from here.
        const isPage = !/\.[^/]*$/.test(url.pathname);
        if (url.origin !== self.location.origin || !(isPage || isHashedAsset(url))) continue;
        if (await cache.match(url.href, { ignoreSearch: true })) continue;
        try {
          await putInShell(cache, url.href, await fetch(url.href));
        } catch {
          // Offline or gone: the fetch handler caches it the next time the page loads it.
        }
      }
    })(),
  );
});

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
// Without a Firebase project (a local build with no EXPO_PUBLIC_FIREBASE_*), the worker still keeps the shell.
if (config.projectId && config.apiKey && config.appId) {
  firebase.initializeApp(config);
  // Initialising messaging makes the SDK display `notification` payloads that arrive in the background.
  firebase.messaging();
}
