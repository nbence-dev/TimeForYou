import { Ionicons } from '@expo/vector-icons';
import { useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Button } from '@/shared/components/button';
import { LessonCard } from '@/shared/components/lesson-card';
import { StatusChip } from '@/shared/components/status-chip';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';
import type { Lesson } from '@/shared/types/lesson';

import { cardStatusFor, dayKey, dayRange, formatDayTitle, formatTime, lessonsByDay, shiftDay } from './lesson-time';
import { useTimetable, type TimetableProblem } from './use-timetable';
import {
  clampDay,
  dayOfMonth,
  daysOfWeek,
  formatGap,
  isWithin,
  lessonDay,
  longWeekday,
  mondayOf,
  nextLessonAfter,
  shortWeekday,
  weeksIn,
} from './week-helpers';

/** How often to ask again while the student has no lessons yet (first poll still running, #58). */
const RETRY_WHILE_EMPTY_MS = 5_000;

/**
 * Browse the whole window `GET /timetable` serves, a week at a time (#60). The window is taken from
 * the lessons themselves, never a length coded here. Only the chosen day's lessons are rendered.
 */
export function TimetableScreen() {
  const insets = useSafeAreaInsets();
  const { lessons, refreshing, problem, refresh } = useTimetable({ retryWhileEmptyMs: RETRY_WHILE_EMPTY_MS });
  const [chosenDay, setChosenDay] = useState<string | null>(null);
  const [pulling, setPulling] = useState(false);

  const today = dayKey(new Date());
  const byDay = useMemo(() => lessonsByDay(lessons), [lessons]);
  const range = useMemo(() => dayRange(lessons, today), [lessons, today]);
  const weeks = useMemo(() => weeksIn(range), [range]);

  // A chosen day that has rolled out of the window snaps back to today.
  const day = chosenDay !== null && isWithin(chosenDay, range) ? chosenDay : today;
  const week = mondayOf(day);
  const weekIndex = Math.max(0, weeks.indexOf(week));
  const dayLessons = byDay.get(day) ?? [];

  const goToDay = (key: string) => setChosenDay(key === today ? null : clampDay(key, range));
  // Moving a week keeps the same weekday, pulled back inside the window at either end.
  const goToWeek = (offset: number) => goToDay(shiftDay(day, offset * 7));

  async function onPullToRefresh() {
    setPulling(true);
    try {
      await refresh();
    } finally {
      setPulling(false);
    }
  }

  return (
    <View style={[styles.screen, { paddingTop: insets.top + Spacing.two }]}>
      <View style={styles.column}>
        <View style={styles.header}>
          <Text style={styles.title} accessibilityRole="header">
            Timetable
          </Text>
          {problem === 'offline' && lessons.length > 0 ? <StatusChip kind="offline" /> : null}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Go to today"
            hitSlop={8}
            onPress={() => goToDay(today)}
            style={styles.iconButton}>
            <Ionicons name="today-outline" size={24} color={Brand.navy} />
          </Pressable>
        </View>

        {lessons.length > 0 ? (<>
        <View style={styles.weekRow}>
          <Arrow direction="back" label="Previous week" disabled={weekIndex === 0} onPress={() => goToWeek(-1)} />
          <Text style={styles.weekTitle}>
            Week {weekIndex + 1} of {weeks.length}
          </Text>
          <Arrow
            direction="forward"
            label="Next week"
            disabled={weekIndex >= weeks.length - 1}
            onPress={() => goToWeek(1)}
          />
        </View>
        

        <View style={styles.progress} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
          {weeks.map((monday, i) => (
            <View
              key={monday}
              style={[styles.segment, i < weekIndex && styles.segmentPast, i === weekIndex && styles.segmentCurrent]}
            />
          ))}
        </View>

        <View style={styles.dayStrip}>
          {daysOfWeek(week).map((key) => (
            <DayChip
              key={key}
              day={key}
              selected={key === day}
              isToday={key === today}
              disabled={!isWithin(key, range)}
              onPress={() => goToDay(key)}
            />
          ))}
        </View>
        </>) : null}
        {refreshing && lessons.length > 0 ? <RefreshingLine /> : null}
      </View>

      <ScrollView
        style={styles.body}
        contentContainerStyle={{ paddingBottom: insets.bottom + Spacing.four }}
        refreshControl={<RefreshControl refreshing={pulling} onRefresh={onPullToRefresh} />}>
        <View style={styles.column}>
          {lessons.length === 0 ? (
            <FetchingState problem={problem} onRetry={refresh} />
          ) : dayLessons.length > 0 ? (
            <DaySchedule day={day} lessons={dayLessons} />
          ) : (
            <EmptyDay day={day} next={nextLessonAfter(lessons, day)} onGoTo={goToDay} />
          )}
        </View>
      </ScrollView>
    </View>
  );
}

function Arrow({
  direction,
  label,
  disabled,
  onPress,
}: {
  direction: 'back' | 'forward';
  label: string;
  disabled: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      hitSlop={8}
      style={[styles.arrow, disabled && styles.arrowDisabled]}>
      <Ionicons name={direction === 'back' ? 'chevron-back' : 'chevron-forward'} size={22} color={Brand.navy} />
    </Pressable>
  );
}

function DayChip({
  day,
  selected,
  isToday,
  disabled,
  onPress,
}: {
  day: string;
  selected: boolean;
  isToday: boolean;
  disabled: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={formatDayTitle(day)}
      accessibilityState={{ selected, disabled }}
      disabled={disabled}
      onPress={onPress}
      style={[
        styles.dayChip,
        isToday && styles.dayChipToday,
        selected && styles.dayChipSelected,
        disabled && styles.dayChipDisabled,
      ]}>
      <Text style={[styles.dayChipName, selected && styles.dayChipTextSelected]}>{shortWeekday(day)}</Text>
      <Text style={[styles.dayChipNumber, selected && styles.dayChipTextSelected]}>{dayOfMonth(day)}</Text>
    </Pressable>
  );
}

function RefreshingLine() {
  return (
    <View style={styles.refreshing}>
      <ActivityIndicator size="small" color={Brand.orange} />
      <Text style={styles.refreshingText}>Refreshing timetable…</Text>
    </View>
  );
}

function DaySchedule({ day, lessons }: { day: string; lessons: Lesson[] }) {
  const cancelled = lessons.filter((l) => l.status === 'cancelled').length;
  const scheduled = lessons.length - cancelled;

  return (
    <View style={styles.dayBlock}>
      <View style={styles.dayHeading}>
        <Text style={styles.dayTitle} accessibilityRole="header">
          {formatDayTitle(day)}
        </Text>
        <Text style={styles.daySummary}>
          {plural(scheduled, 'scheduled session')}
          {cancelled > 0 ? <Text style={styles.cancelledCount}> · {plural(cancelled, 'cancellation')}</Text> : null}
        </Text>
      </View>

      {lessons.map((lesson, i) => (
        <View key={lesson.id} style={styles.lessonBlock}>
          {i > 0 ? <GapRow from={lessons[i - 1].end_ts} to={lesson.start_ts} /> : null}
          {lesson.status === 'cancelled' ? <StatusChip kind="cancelled" /> : null}
          <LessonCard
            status={cardStatusFor(lesson, 'upcoming')}
            start={formatTime(lesson.start_ts)}
            end={formatTime(lesson.end_ts)}
            title={lesson.subject}
            room={lesson.room}
            teacher={lesson.lecturer}
          />
        </View>
      ))}
    </View>
  );
}

/** "30m free (09:30 – 10:00)" between two lessons; nothing when they touch or overlap. */
function GapRow({ from, to }: { from: string; to: string }) {
  const minutes = Math.round((Date.parse(to) - Date.parse(from)) / 60_000);
  if (!(minutes > 0)) return null;

  return (
    <View style={styles.gapRow}>
      <View style={styles.gapLine} />
      <View style={styles.gapPill}>
        <Ionicons name="time-outline" size={14} color={Colors.light.textSecondary} />
        <Text style={styles.gapText}>
          {formatGap(minutes)} free ({formatTime(from)} – {formatTime(to)})
        </Text>
      </View>
      <View style={styles.gapLine} />
    </View>
  );
}

function EmptyDay({ day, next, onGoTo }: { day: string; next: Lesson | null; onGoTo: (key: string) => void }) {
  const nextDay = next ? lessonDay(next) : null;

  return (
    <View style={styles.emptyState}>
      <View style={styles.emptyIcon}>
        <Ionicons name="cafe-outline" size={36} color={Brand.navy} />
      </View>
      <Text style={styles.emptyTitle}>No lessons on {longWeekday(day)}</Text>
      {next && nextDay ? (
        <>
          <Text style={styles.emptyText}>
            Next lesson:{' '}
            <Text style={styles.emptyStrong}>
              {formatDayTitle(nextDay)}, {formatTime(next.start_ts)}
            </Text>{' '}
            {next.subject}.
          </Text>
          <Button label={`Go to ${longWeekday(nextDay)}`} variant="secondary" onPress={() => onGoTo(nextDay)} />
        </>
      ) : (
        <Text style={styles.emptyText}>Nothing else is scheduled in this window.</Text>
      )}
    </View>
  );
}

/** No lessons at all yet: the first poll is still running, or we're offline with nothing saved. */
function FetchingState({ problem, onRetry }: { problem: TimetableProblem; onRetry: () => void }) {
  if (problem === 'session') {
    return (
      <View style={styles.emptyState}>
        <Text style={styles.emptyTitle}>Your session has expired</Text>
        <Text style={styles.emptyText}>Link your account again to see your timetable.</Text>
      </View>
    );
  }

  const offline = problem === 'offline';
  return (
    <View style={styles.emptyState} accessibilityLiveRegion="polite">
      {offline ? (
        <Ionicons name="cloud-offline-outline" size={40} color={Colors.light.textSecondary} />
      ) : (
        <ActivityIndicator size="large" color={Brand.navy} />
      )}
      <Text style={styles.emptyTitle}>{offline ? "You're offline" : 'Fetching your timetable'}</Text>
      <Text style={styles.emptyText}>
        {offline
          ? 'Nothing is saved on this device yet. Your timetable will appear once you are back online.'
          : 'Your lessons are on their way. This usually takes a few seconds after you link your account.'}
      </Text>
      {offline || problem === 'failed' ? <Button label="Try again" variant="secondary" onPress={onRetry} /> : null}
    </View>
  );
}

function plural(n: number, noun: string): string {
  return `${n} ${noun}${n === 1 ? '' : 's'}`;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  column: {
    width: '100%',
    maxWidth: MaxContentWidth,
    alignSelf: 'center',
    paddingHorizontal: Spacing.three,
    gap: Spacing.three,
  },
  header: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  title: { flex: 1, fontFamily: Font.textBold, fontSize: 28, color: Brand.navy },
  iconButton: { padding: Spacing.one },
  weekRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  weekTitle: { flex: 1, textAlign: 'center', fontFamily: Font.textBold, fontSize: 20, color: Brand.navy },
  arrow: { width: 40, height: 40, borderRadius: Radius.pill, alignItems: 'center', justifyContent: 'center' },
  arrowDisabled: { opacity: 0.3 },
  progress: { flexDirection: 'row', gap: Spacing.two },
  segment: { flex: 1, height: 6, borderRadius: Radius.pill, backgroundColor: Colors.light.backgroundSelected },
  segmentPast: { backgroundColor: Brand.navy },
  segmentCurrent: { backgroundColor: Brand.orange },
  dayStrip: { flexDirection: 'row', gap: Spacing.two },
  dayChip: {
    flex: 1,
    alignItems: 'center',
    gap: Spacing.half,
    paddingVertical: Spacing.two,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  dayChipToday: { borderColor: Brand.navy },
  dayChipSelected: { backgroundColor: Brand.orange, borderColor: Brand.orange },
  dayChipDisabled: { opacity: 0.35 },
  dayChipName: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  dayChipNumber: {
    fontFamily: Font.labelSemiBold,
    fontSize: 18,
    color: Colors.light.text,
    fontVariant: ['tabular-nums'],
  },
  dayChipTextSelected: { color: Brand.white },
  refreshing: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  refreshingText: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  body: { flex: 1, marginTop: Spacing.three },
  dayBlock: { gap: Spacing.three, paddingTop: Spacing.two },
  dayHeading: { gap: Spacing.half },
  dayTitle: { fontFamily: Font.textBold, fontSize: 22, color: Colors.light.text },
  daySummary: { fontFamily: Font.label, fontSize: 14, color: Colors.light.textSecondary },
  cancelledCount: { color: Status.cancelled.text },
  lessonBlock: { gap: Spacing.two },
  gapRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  gapLine: { flex: 1, height: 1, backgroundColor: Brand.border },
  gapPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.one,
    paddingHorizontal: Spacing.three,
    paddingVertical: Spacing.one,
    borderRadius: Radius.pill,
    borderWidth: 1,
    borderColor: Brand.borderStrong,
    backgroundColor: Brand.white,
  },
  gapText: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary, fontVariant: ['tabular-nums'] },
  emptyState: { alignItems: 'center', gap: Spacing.three, paddingVertical: Spacing.six, paddingHorizontal: Spacing.three },
  emptyIcon: {
    width: 96,
    height: 96,
    borderRadius: Radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.backgroundElement,
  },
  emptyTitle: { fontFamily: Font.textBold, fontSize: 20, color: Colors.light.text, textAlign: 'center' },
  emptyText: {
    fontFamily: Font.text,
    fontSize: 15,
    lineHeight: 22,
    color: Colors.light.textSecondary,
    textAlign: 'center',
  },
  emptyStrong: { fontFamily: Font.textBold, color: Colors.light.text },
});