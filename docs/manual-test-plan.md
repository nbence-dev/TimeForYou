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

## Add to Home Screen (#67)

A2HS-1 and A2HS-3 need a real iPhone: iOS only opens the app full-screen (and delivers Web Push)
from the Home Screen, which a desktop browser cannot fake. A2HS-2 and A2HS-4 can also be checked
on the web with DevTools device mode set to an iPhone (reload after switching).

### A2HS-1 Guide after linking on iPhone Safari

**Setup:** A real iPhone, TimeForYou not on the Home Screen, device not linked.

**Steps:**

1. Open the app in Safari and scan the QR code.
2. Tap "Skip for now".

**Expected:**

- After the scan, the "Turn on notifications" guide shows instead of the Today screen.
- "Skip for now" goes to the Today screen, which shows the "Notifications are off" banner.

### A2HS-2 Banner on the Today screen

**Setup:** iPhone Safari (or DevTools iPhone mode), linked, not on the Home Screen.

**Steps:**

1. Open the Today screen and tap "Fix Settings →" on the banner.
2. Tap "Skip for now", then tap ✕ on the banner.
3. Reload the page.

**Expected:**

- "Fix Settings →" opens the guide; "Skip for now" returns to Today with the banner still there.
- ✕ hides the banner, and it stays hidden after the reload. It comes back in a new tab.

### A2HS-3 Opened from the Home Screen

**Setup:** A real iPhone, linked in Safari.

**Steps:**

1. In Safari, tap Share › Add to Home Screen.
2. Open TimeForYou from the new Home Screen icon.

**Expected:**

- The app opens full-screen, with no Safari address bar.
- No banner and no guide. The icon is labelled "TimeForYou".

### A2HS-4 Not shown off iPhone

**Setup:** A desktop browser or an Android phone, linked.

**Steps:**

1. Open the Today screen.

**Expected:**

- No "Notifications are off" banner, and linking goes straight to the Today screen.