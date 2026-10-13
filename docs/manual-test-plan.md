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

### TT-3 Lessons refresh after a change (#130)

**Setup:** A linked device with push working, on Timetable (then repeat on Today). The demo page
from #103 open on the laptop.

**Steps:**
1. In the demo page, change the end time of a lesson that is on screen, then **Nudge now**.
2. Without reloading or pulling down, wait for the push.
3. Repeat, but this time put the app in the background (or switch to another tab) before the
   nudge, and bring it back afterwards.
4. Repeat once more with the API stopped, then bring the app back.

**Expected:**
- Steps 2 and 3: the lesson shows its new end time and the Changes badge appears, with no manual
  refresh.
- Step 4: the lessons stay on screen. They are not replaced by an error state.

## Add to Home Screen (#67)

A2HS-1 and A2HS-3 need a real iPhone: iOS only opens the app full-screen (and delivers Web Push)
from the Home Screen, which a desktop browser cannot fake. A2HS-2 and A2HS-4 can also be checked
on the web with DevTools device mode set to an iPhone (reload after switching).

### A2HS-1 Guide before linking on iPhone Safari

**Setup:** A real iPhone, TimeForYou not on the Home Screen, device not linked.

**Steps:**

1. Open the app in Safari.
2. Tap "Skip for now".
3. Scan the QR code.

**Expected:**

- Opening the app shows the "Turn on notifications" guide first, before the scan screen.
- "Skip for now" goes to the scan screen. After the scan, the Today screen shows the "Notifications
  are off" banner.
- In Chrome on the iPhone, the guide starts with an extra "Open this page in Safari" step.

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

**Setup:** A real iPhone, not linked, TimeForYou not on the Home Screen.

**Steps:**

1. In Safari, follow the guide: Share › Add to Home Screen.
2. Open TimeForYou from the new Home Screen icon.
3. Scan the QR code.

**Expected:**

- The app opens full-screen, with no Safari address bar, and shows the scan screen (not the guide).
- After the scan the Today screen shows, with no banner and no guide. The icon is the tfy logo,
  labelled "TimeForYou".
- Closing and reopening the app from the icon stays signed in.

### A2HS-4 Not shown off iPhone

**Setup:** A desktop browser or an Android phone, linked.

**Steps:**

1. Open the Today screen.

**Expected:**

- No "Notifications are off" banner, and linking goes straight to the Today screen.

## Web push registration (#69)

Needs a real iPhone (iOS 16.4+) with the PWA added to the Home Screen, the Firebase web config in
`app/.env.local` (see `.env.example`), and the API reachable from the phone.

### PUSH-1 Grant permission and register

**Steps:** Open the installed PWA, tap "Turn on notifications", allow.

**Expected:** The strip disappears without tapping its ✕, Settings → Notification status reads
"On" without reloading, and `devices` holds one `web_push` row for the user. Reopening the app
does not add a second row. If the red "Notifications are off. Add TimeForYou to your Home Screen"
banner (A2HS-2) was showing, it goes away too once notifications are on.

### PUSH-2 Denied permission

**Steps:** Tap "Turn on notifications", refuse.

**Expected:** The app stays fully usable and shows "Notifications are off" with how to re-enable.

### PUSH-3 Delivery and tap (needs the real FCM sender on the server)

**Steps:** Change a lesson in the mock, `POST /nudge`, wait for the push, tap it.

**Expected:** One grouped notification arrives; tapping opens the Changes tab.

## Demo controller (#103)

### DEMO-1 Change lessons and nudge from the page

**Setup:** Stack up, one linked device with notifications on. Open `http://localhost:8000/demo`
(on tfy-prod: `ssh -L 8000:localhost:8000 nick@192.168.3.61` first).

**Steps:**

1. Confirm the page lists the seeded lessons with id, subject, time, room and status.
2. Press **Cancel** on one lesson, then **Nudge now**.
3. Type a room on another lesson and press **Change room**; press **Move** on a third with a new time.
4. Press **Restore** on the cancelled lesson.
5. Press **Reset mock** and confirm the prompt.
6. Stop the worker (`docker compose stop worker`) and press **Nudge now**.

**Expected:** Each action updates the table and the status banner shows success. Step 2 delivers one
grouped push to the device. Step 6 shows an error in the banner, not a silent failure. Over the public
hostnames, `/demo` does not return the page.

### DEMO-2 One timetable per developer, and guests (#129)

**Setup:** Stack up, `/demo` open on the LAN, the PWA installed on two phones with notifications on.

**Steps:**

1. In the **Student** dropdown pick a developer (their GitHub handle). Confirm the lesson list and the
   QR code change. Pick a different developer and confirm the timetable differs.
2. On phone A, scan the QR code of one developer (**Scan** in the app's onboarding).
3. In **Add a guest** type a name and surname and press **Create**. Confirm the guest is selected and
   a QR code appears.
4. On phone B, scan the guest's QR code. Open Settings, copy the calendar link and add it to a
   calendar app.
5. With the guest selected, **Cancel** a lesson, then press **Nudge now**.
6. Select the developer linked on phone A and confirm their lessons are untouched.

**Expected:** Phone B shows the guest's own timetable and receives one grouped push; phone A receives
nothing. The calendar feed on phone B shows the cancelled lesson as cancelled. Guests live in memory:
restarting `mock` removes them.

## Android native push (#70)

Needs a real Android phone running the Expo build (`npx expo run:android` with the device attached,
since Expo Go cannot receive FCM), and `app/google-services.json` from the Firebase console
(Android app `com.timeforyou.app`). The file is gitignored; never commit it.

### AND-1 Grant permission and register

**Steps:** Open the app, tap "Turn on notifications", allow.

**Expected:** The strip disappears and `devices` holds one `android_fcm` row. Reopening the app
does not add a second row.

### AND-2 Denied permission

**Steps:** Tap "Turn on notifications", refuse (twice if Android asks again).

**Expected:** The app stays fully usable. Once Android will not ask again, the "Notifications are
off" strip appears.

### AND-3 Delivery with the app closed, and tap

**Steps:** Swipe the app away. Change a lesson in the mock, `POST /nudge`, wait for the push, tap it.

**Expected:** One grouped notification arrives; tapping opens the Changes tab.

## Notes (#73)

### NT-1 Add, edit and delete a note

**Setup:** A linked device that has a lesson on Today or Timetable.

**Steps:**

1. Tap a lesson to open its sheet. It shows the lesson and "No notes on this lesson yet."
2. Tap **Add note**, type "Bring laptop charger", tap **Save note**.
3. Tap the pencil on the note, add " and a pen", tap **Save note**.
4. Tap the trash icon on the note, then **Delete** on the "Delete this note?" prompt.

**Expected:**

- After step 2 you are back on the lesson sheet and the note is listed straight away, marked
  "Created just now · Private to you".
- After step 3 the note shows the edited text.
- After step 4 the note is gone and the empty message returns. Tapping **Cancel** on the prompt
  instead keeps the note.

### NT-2 A note written on one device shows on another

**Setup:** Link two devices (or two browsers) to the same student by scanning the same QR code on
each. The second scan gives a second token for the same user.

**Steps:**

1. On device A add a note to a lesson.
2. On device B open that lesson's sheet (or close and reopen it).
3. On device B add a second note, then reopen the sheet on device A.

**Expected:**

- Device B lists device A's note, and device A lists device B's, each after the sheet is opened.
  No restart is needed.

### NT-3 Empty and over-length notes are blocked

**Steps:**

1. Open **Add note** and tap **Save note** without typing, then with only spaces.
2. Type or paste more than 500 characters.

**Expected:**

- An empty or spaces-only note shows "Write something before saving." and nothing is sent.
- The input stops accepting characters at 500 and the counter reads "500 / 500". If a longer text
  somehow gets in, saving shows "A note can be at most 500 characters."

### NT-4 A failed save keeps the text

**Steps:**

1. Open **Add note** and type "This text must survive a failed save".
2. Take the device offline (airplane mode, or stop the `api` container when testing on the web).
3. Tap **Save note**.
4. Go back online and tap **Save note** again.

**Expected:**

- Step 3 shows "Couldn't reach the server. Your note is still here: try again when you're
  online." and the typed text is still in the box.
- Step 4 saves the note and returns to the lesson sheet, where it is listed.
- With the device offline, opening a lesson's sheet shows the lesson but says the notes can't be
  shown right now, with **Try again**. Notes are online only until the Week 4 offline queue.

## Changes screen (#71)

Set `POLL_INTERVAL_SECONDS=30` in `.env` (then `docker compose up -d`) so a mock change is picked up
within a minute instead of ten.

### CH-1 No changes

**Setup:** A linked device; the mock reset (`POST /mock/reset`) and no changes in the last 30 days.

**Steps:**

1. Open the Changes tab.

**Expected:**

- "You're all caught up" with a tick, a "View timetable" button and "Last checked HH:MM".
- "View timetable" opens the Timetable tab.

### CH-2 Several changes at once

**Setup:** A linked device. Pick three upcoming lessons from `GET /timetable` (their `untis_lesson_id`).

**Steps:**

1. In one go, call `POST /mock/cancel-lesson/{id}` for the first, `POST /mock/change-room/{id}`
   with `{"room": "Z99"}` for the second, and `POST /mock/move-lesson/{id}` (one hour later) for
   the third.
2. Wait for the next poll, then open the Changes tab (or tap ↻).

**Expected:**

- The summary reads "3 changes in your timetable" with 1 cancelled, 1 room, 1 time.
- All three cards show, newest first: the cancelled lesson struck through on a red card, then
  "Room: old → Z99", then "Time: old → new" one hour later.
- Each card names the lesson and when it is.

### CH-3 Opened by route

**Steps:**

1. With the app open on another tab, go to `/changes` directly (type it in the address bar on the
   web, or tap a change notification).

**Expected:**

- The Changes screen opens and loads the latest changes.

## Changed-lesson marker (#72)

### CM-1 A changed lesson shows a dot and the Changes tab shows a count

**Setup:** A linked device that has already opened the app once (the first launch marks nothing,
however much history exists). The lesson to change is on a day the app shows.

**Steps:**

1. On the mock, change a lesson's room, using the lesson's mock id (template id and date, e.g.
   `1006-2026-10-08`): `POST /mock/change-room/1006-2026-10-08` with `{"room":"Z77"}`. Pick a room
   the lesson does not already have, or the poll finds nothing to report.
2. Wait for the next poll, or `POST /nudge` on the worker.
3. Open the app again (a reload on the web).
4. On a different day, look at a lesson that did not change.

**Expected:**

- The changed lesson shows a small orange dot at the top right of its card, on Today and on
  Timetable, and the Changes tab shows an orange badge with the number of unseen changes.
- The lesson that did not change shows no dot.

### CM-2 A marker stays until the student has seen the change

**Steps:**

1. Do CH-1 with changes on two lessons, one on a day other than today, so the dots and a badge of 2
   are showing.
2. Close and reopen the app (a reload on the web) without tapping anything. Repeat with the device
   offline (stop the `api` container on the web).
3. Tap the lesson that has the dot on Today, then go back.
4. Close and reopen the app again.
5. Tap the **Changes** tab, then close and reopen the app once more.

**Expected:**

- After step 2 the dot and the badge are still there, online and offline: nothing has been seen
  yet, so a restart does not clear them.
- After step 3 that lesson's dot is gone and the badge drops by its changes. The other lesson's
  change is still counted.
- After step 4 the opened lesson stays clear and the other change is still marked.
- After step 5 the badge is gone and stays gone. A change made after that shows on its own.
- The very first launch on a device shows no dots even if the student has weeks of change
  history.

### CM-3 Signing out forgets what was seen

**Steps:**

1. Use the app until a changes list has been fetched.
2. Make the login stop working (on the web, replace the stored `timeforyou.token` with any other
   text) and reload, or link another account by scanning.

**Expected:**

- The app lands on the scan screen. On the web, the `timeforyou.kv.changes.*` entries in Local
  Storage are gone, so the next student starts with no markers and none of the previous student's
  changes.


## Settings screen and unlinking (#108)

### ST-1 The layout, and the "Soon" rows do nothing

**Steps:**

1. Sign in and open the **Settings** tab.
2. Tap Linked devices.

**Expected:**

- The screen shows the profile card, then Notifications, Calendar, Devices, Data and About sections and an
  **Unlink this device** button.
- Linked devices is dimmed with a "Soon" tag, and tapping it does nothing.
- Notification status shows On, Off, Not set up, Unavailable or Error to match this device. Version shows
  the app version.

### ST-2 Sync now

**Steps:**

1. Open Settings. Note that Last synced fills in once the first fetch finishes.
2. Make a change in the mock (for example cancel a lesson), then tap **Sync now**.
3. Stop the `api` container and tap **Sync now** again.

**Expected:**

- After step 2 Last synced moves forward, the Changes tab lists the new change and shows its badge,
  and the cancelled lesson shows on the Timetable and Today tabs without pulling to refresh them.
- After step 3 the chip says Offline and Last synced keeps its earlier time.

### ST-3 Unlink and link again

**Steps:**

1. Add a note to a lesson, then open Settings and tap **Unlink this device**, then **Cancel**.
2. Tap **Unlink this device**, then **Unlink**.
3. Scan the same QR again.

**Expected:**

- Cancel leaves the student on Settings, still signed in.
- After step 2 the app lands on the scan screen, and the device is gone from the `devices` table.
- After step 3 the timetable returns and the note from step 1 is still there.

### ST-4 Unlink offline

**Steps:**

1. Stop the `api` container, open Settings, tap **Unlink this device**, then **Unlink**.

**Expected:**

- A message says the student is offline and still signed in. They stay on Settings, and the
  timetable and notes are untouched. With the connection back, **Unlink** works.

## Calendar subscription (#109)

### CS-1 Show and copy the feed link

**Steps:**

1. Open **Settings** and tap **Calendar subscription**.
2. Tap **Copy link**, then paste somewhere (the address bar will do).
3. Open the pasted address in a browser.
4. Sign in on a second device (or scan the same QR again) and open the same screen.

**Expected:**

- The screen shows a long `.../ics/<token>.ics` address, and the button says **Copied** for a moment
  after the tap.
- What you paste is exactly that address, and the browser gets a calendar file (it downloads or
  opens as `text/calendar`).
- The second device shows the same address.
- Add to Apple Calendar, Add to Google Calendar and Reset link are dimmed with a "Soon" tag and do
  nothing. The note says anyone with the link can see the timetable.

### CS-2 Offline

**Steps:**

1. Stop the `api` container, then open **Calendar subscription**.
2. Start the container again and tap **Try again**.

**Expected:**

- Step 1 shows "You're offline…" with a **Try again** button, not a blank screen.
- Step 2 shows the address.

## Profile card and Help (#113)

### PF-1 The profile card shows the student's name

**Setup:** A linked device; the worker has polled at least once since linking.

**Steps:**

1. Open the **Settings** tab.

**Expected:**

- The card at the top shows the student's initials in a circle, and their name from WebUntis
  (for the mock's demo student: "DS", "Demo Student").
- "Sync status" shows a green "Synced HH:MM" pill, a red "Offline" or "Couldn't sync" pill, or a grey
  "Syncing…" pill while a sync runs.

### PF-2 Before the first poll

**Setup:** Link a new device and open Settings straight away, before the worker has polled (or stop the
`worker` container first).

**Expected:**

- The card shows a person icon, "Your profile" and "Your name appears after the first sync",
  and fills in after the next poll and a reload.

### HF-1 Help and FAQ

**Steps:**

1. In **Settings**, tap **Help and FAQ**.
2. Tap the back arrow.

**Expected:**

- A Help and FAQ screen opens with the questions and answers; the back arrow returns to Settings.

### CS-3 One-tap add to Apple or Google Calendar (#112)

**Steps:**

1. Open **Calendar subscription** on a phone and tap **Add to Apple Calendar** (iPhone) or **Add to
   Google Calendar**.

**Expected:**

- The Calendar app (iPhone) offers to subscribe to the feed, or Google Calendar opens in the browser
  with the feed address filled in. Accept, and the timetable shows in the calendar.
- On a device with no calendar app to handle it, a message says to copy the link and add it by hand.
- Google Calendar can only reach a public address, so on a local stack (`localhost`) it will not
  load the feed. Check this against the live server (#126).

### CS-4 Reset link (#112)

**Steps:**

1. Open **Calendar subscription**, copy the link, and open it in a browser (a calendar file).
2. Tap **Reset link**, then **Cancel**. Then tap **Reset link** and confirm.
3. Open the old link again, then copy the new one and open that.
4. With the API stopped, tap **Reset link** and confirm.

**Expected:**

- Cancel changes nothing. Confirming shows a new link and the note "New link ready…".
- The old link now gives a "not found" page and the new link gives the calendar file. Another device
  of the same student shows the new link too.
- Offline, a message says so and that the current link still works. The link does not change.

### CS-5 The feed on the live server, in real calendar apps (#126)

Run against the shared instance (the installed Android app, or the web app at
`https://app.timeforyou.co.za`), not a local stack: Google Calendar and Apple Calendar can only reach a
public address, so `localhost` proves nothing.

**Steps:**

1. In the app, open **Settings → Calendar subscription** and note the address shown.
2. Open that address in a browser on a device that is **not** on the team's network (mobile data).
3. On an iPhone, tap **Add to Apple Calendar**, accept the subscription, and open the Calendar app.
4. In Google Calendar, tap **Add to Google Calendar** (or **Other calendars → From URL** with the same
   address) and let it add the feed.
5. In the mock demo page (`/demo`, LAN only), cancel one lesson that is still in the future and press
   **Nudge now**. Wait for the calendar app's next refresh (Apple: Settings → Calendar → Accounts →
   Fetch New Data; Google: it refreshes on its own, which can take hours).

**Expected:**

- The address starts with `https://api.timeforyou.co.za/ics/` and ends with `.ics` (not `localhost`, not
  `http://`, not an internal host name).
- Step 2 downloads or shows a calendar file (`BEGIN:VCALENDAR`), not a sign-in or "not found" page.
- After steps 3 and 4 the student's future lessons show in both calendars, with the room as the event
  location and any note in the description.
- After step 5 the cancelled lesson shows as cancelled (struck through or marked) once the calendar app
  refreshes. Google's refresh delay is not a bug.

**Last checked (2026-10-13), the parts that need no phone:**

- `GET /calendar` on the live server returned an `https://api.timeforyou.co.za/ics/…` address, so
  `PUBLIC_BASE_URL` is set correctly there. `deploy/release.compose.yml` passes it through from the VM's
  `.env`.
- A made-up feed token on `https://api.timeforyou.co.za/ics/…` gets the API's own JSON "No calendar feed
  matches this token" (404) over valid HTTPS through Cloudflare, so the route is reachable publicly.
  (On `app.timeforyou.co.za` the same path returns the web app, which is fine: the app's link uses the
  `api.` host.)

**Still to run by hand:** steps 2 to 5 (a real feed over mobile data, Apple Calendar, Google Calendar and
the cancelled-lesson refresh).

## Changes archive (#138)

### ARC-1 Mark all as read, and the Archive

**Steps:**

1. Make a few changes (for example cancel three lessons on the demo page, then **Nudge now**) and open
   the **Changes** tab.
2. Note the **New** and **Archive** counts at the top, then tap **Mark all as read**.
3. Tap **Archive**.
4. Cancel one more lesson, **Nudge now**, and come back to the Changes tab (switch tab and back, or
   pull down).

**Expected:**

- After step 2 **New** shows 0 with "You're all caught up", and **Archive** holds all the changes that
  were on screen. The button is not shown when there is nothing new.
- Step 3 lists the archived changes newest first, as the same cards (lesson, change type, old and new
  value). With nothing archived it says "Nothing archived yet".
- After step 4 the new change is under **New** and the archive is unchanged.

### ARC-2 The archive stays on this device, and goes on sign-out

**Steps:**

1. With some changes archived, close and reopen the app (a reload on the web).
2. Sign out (Settings → **Unlink this device**) and link again.

**Expected:**

- After step 1 the same changes are still in **Archive**.
- After step 2 **Archive** is empty: the next student sees none of it (on the web the
  `timeforyou.kv.changes.archivedIds` entry in Local Storage is gone).

### ARC-3 Archiving does not touch the orange dots

**Steps:**

1. Change a lesson, **Nudge now**, and look at Today or Timetable: the lesson has an orange dot and the
   Changes tab has a badge.
2. Without opening the Changes tab, note the dots. Then open Changes and tap **Mark all as read**.

**Expected:**

- Opening the Changes tab clears the dots and the badge, exactly as before (#72). **Mark all as read**
  changes nothing about them.
