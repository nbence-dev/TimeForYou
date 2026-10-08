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