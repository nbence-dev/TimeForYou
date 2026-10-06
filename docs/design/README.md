# TimeForYou design standard

How the app looks, and how to keep it looking like one app. Ticket: #80.

The designs were made in the Stitch project "TimeForYou — Phase 1 reference". **The copies in
[`screens/`](./screens/) are what you build from.** Stitch links are not stable and are not
the reference.

## What binds and what does not

| Fixed — follow it | Free — use judgement |
| --- | --- |
| Tokens from `app/src/shared/theme/theme.ts` | Exact pixel positions |
| Shared components (`LessonCard`, `StatusChip`, `Button`, `BottomNav`, #81) | Sample copy and fake data in the mockups |
| The layout and the order of things on a screen | Small spacing differences |
| The bottom nav in [`screens/nav.png`](./screens/nav.png) | |

Not every screen shows the current nav. Stitch left some screens with an older bottom bar.
**`nav.png` is the only nav standard**, with one correction: `nav.png`, `today.png` and
`today-offline.png` label the second tab "Week". **The label is "Timetable".** The four tabs are
Today, Timetable, Changes, Settings.

If a mockup is wrong or cannot be built, build the sensible thing and say so in the PR. Do not
quietly invent a new style.

## Tokens

All values live in `app/src/shared/theme/theme.ts`. **Never write a hex colour in a feature.**

| Token | Value | Use |
| --- | --- | --- |
| `Brand.navy` | `#15315D` | App bars, primary buttons, active nav, headings |
| `Brand.orange` | `#CC5A01` | Accent only: the one hero action per screen, active weekday, current-time line, unseen-change dot, focus ring, Changes badge |
| `Status.onTime` | strip `#059669` | Lesson on time, synced, confirmed |
| `Status.changed` | strip `#A16207`, chip `#FEF9C3` | Room or time changed. Always with a chip label and an icon |
| `Status.cancelled` | strip `#DC2626`, chip `#FEE2E2` | Cancelled lesson, errors, destructive actions |
| `Status.live` | strip `#CC5A01` | Lesson happening now |
| `Colors.light.*` | slate neutrals | Text, canvas, surfaces |
| `Spacing` | 4px base unit | 16px (`three`) outer margin on every screen |
| `Radius` | `base` 8, `card` 16, `pill` full | Inputs and tags, cards and sheets, chips and day selectors |

Orange and the "changed" gold are deliberately different. Orange means "this is the thing to
tap". Gold means "this lesson moved". Never use orange for a change, or gold for an action.

## Type

Hanken Grotesk for text and headings. Public Sans for times, rooms, course codes and chip
labels, with tabular numerals. Names are in `FontFamily`; loading the fonts is part of #81.

## Components

Built once in `app/src/shared/components/` (#81). Do not build your own.

- **`LessonCard`** — 4px left strip in the status colour. Row 1: time range and course code chip.
  Row 2: title. Row 3: room and teacher. Note icon when the lesson has a note; small orange dot
  when the change is unseen. Cancelled: struck-through title on a `#FEF2F2` background.
- **`StatusChip`** — 24px, fully rounded. Cancelled, Room changed, Time changed, Live now,
  Offline.
- **`Button`** — primary (navy), accent (orange, one per screen), secondary, destructive.
  48px minimum height.
- **`BottomNav`** — inset white pill with a recessed track. Four tabs: Today, Timetable, Changes,
  Settings. The active tab is a navy capsule with a white icon and label. The Changes tab shows
  an orange count badge when there are unseen changes.
- **Banners** — 12px radius, dismissable. Built by the ticket that needs them (#67 first).

## Screens and the tickets that build them

Screens marked `*` may show an older bottom bar. Follow `nav.png`.

| Screen file | Built by |
| --- | --- |
| `welcome.png`, `scan.png`, `linking.png`, `scan-error.png` | #66 |
| `add-to-home-screen.png`, `today-offline.png` (notifications-off banner) | #67 |
| `notification-permission.png`, `lock-screen-notification.png` | #69, #70 |
| `today.png`*, `today-offline.png` | #68 |
| `timetable.png`*, `timetable-empty.png`* | #60 |
| `changes.png`*, `changes-empty.png`* | #71 |
| `lesson-detail.png`, `note-edit.png` | #73 |
| `today.png`, `timetable.png` (unseen-change dot) | #72 |
| `nav.png` | #81 |
| `settings.png`*, `calendar-subscription.png`*, `linked-devices.png`* | Not yet ticketed. Later waves |

Screens are PNGs to look at. They are pictures, not code to copy.

## Before you open a UI PR

- It looks like the screen named in your ticket's `Design:` line.
- Colours, spacing and radii come from `theme.ts`. Components come from `shared/components/`.
- You attached a screenshot or recording (the PR template already asks).

Reviewers use judgement. A small difference is fine. A new one-off card or a hard-coded hex is
not.

## Changing the design

Change the design by changing the screen file and this README in a docs PR first, then the
code. Do not let the code drift away from the pictures.
