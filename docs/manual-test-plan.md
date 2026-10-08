# Manual test plan

Frontend automated tests are a deliberate scope cut (#3), so the app is checked by hand with the
cases below. Run the relevant section before opening a UI PR, and add the cases your ticket asks
for.

Each case: **setup**, **steps**, **expected**. Unless a case says otherwise, start from a linked
device with the backend running and the mock reset (`POST /mock/reset`).

## Timetable (#60)

### TT-1 Browse to week 7 offline

**Setup:** The device has fetched the timetable at least once while online.

**Steps:**

1. Take the device offline (airplane mode, or stop the `api` container when testing on the web).
2. Reload the app and open the Timetable tab.
3. Tap › until the header reads "Week 7 of 7". Pick a weekday with lessons.

**Expected:**

- The Offline chip shows next to the title.
- Week 7's lessons are listed, the same as when online.
- › is disabled on the last week, and days after the last served day are greyed out and cannot be
  tapped.

### TT-2 First-registration empty state

**Setup:** Nothing cached on the device (fresh install, or clear the app's storage).

**Steps:**

1. Link the device (scan the QR code) and open the Timetable tab straight away, before the first
   poll has finished. On the web, an easy way to reproduce it is to stop the `api` container and
   clear the `timeforyou-cache` IndexedDB database, then reload.
2. Wait without touching the app.

**Expected:**

- A full-screen "Fetching your timetable" state shows (or "You're offline" if there is no
  connection), not a blank screen. The week bar and day chips are hidden.
- Once lessons are available, the screen replaces the empty state with this week's lessons
  within a few seconds, with no reload or restart.

## Web push registration (#69)

Needs a real iPhone (iOS 16.4+) with the PWA added to the Home Screen, the Firebase web config in
`app/.env.local` (see `.env.example`), and the API reachable from the phone.

### PUSH-1 Grant permission and register

**Steps:** Open the installed PWA, tap "Turn on notifications", allow.

**Expected:** The strip disappears and `devices` holds one `web_push` row for the user. Reopening
the app does not add a second row.

### PUSH-2 Denied permission

**Steps:** Tap "Turn on notifications", refuse.

**Expected:** The app stays fully usable and shows "Notifications are off" with how to re-enable.

### PUSH-3 Delivery and tap (needs the real FCM sender on the server)

**Steps:** Change a lesson in the mock, `POST /nudge`, wait for the push, tap it.

**Expected:** One grouped notification arrives; tapping opens the Changes tab.
