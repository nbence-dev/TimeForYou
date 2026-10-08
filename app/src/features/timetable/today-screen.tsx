import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Button } from '@/shared/components/button';
import { LessonCard } from '@/shared/components/lesson-card';
import { StatusChip } from '@/shared/components/status-chip';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';
import type { Lesson } from '@/shared/types/lesson';

import {
  cardStatusFor,
  clampDay,
  dayKey,
  dayRange,
  formatDayTitle,
  formatTime,
  lessonPhase,
  lessonsByDay,
  shiftDay,
} from './lesson-time';
import { useTimetable, type TimetableProblem } from './use-timetable';

/** Re-evaluates "now" so the live and finished lessons update while the screen stays open. */
function useNow(intervalMs: number): Date {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}

/** The landing screen: today's lessons in time order, from the cache first. */
export function TodayScreen() {
  const insets = useSafeAreaInsets();
  const { lessons, cacheRead, refreshing, problem, syncedAt, refresh } = useTimetable();
  const now = useNow(30_000);
  const [pulling, setPulling] = useState(false);
  const [chosenDay, setChosenDay] = useState<string | null>(null);

  const today = dayKey(now);
  const byDay = useMemo(() => lessonsByDay(lessons), [lessons]);
  const range = useMemo(() => dayRange(lessons, today), [lessons, today]);
  // A refresh can shrink the range under a chosen day; keep the view inside the days that exist.
  const day = clampDay(chosenDay ?? today, range);
  const dayLessons = byDay.get(day) ?? [];

  const isToday = day === today;
  const remaining = isToday
    ? dayLessons.filter((l) => l.status !== 'cancelled' && lessonPhase(l, now) !== 'past').length
    : dayLessons.length;

  async function onPullToRefresh() {
    setPulling(true);
    try {
      await refresh();
    } finally {
      setPulling(false);
    }
  }

  // Stepping onto today goes back to following today, so a screen left open past midnight rolls over
  // to the new day instead of staying pinned to yesterday.
  const goToDay = (key: string) => setChosenDay(key === today ? null : key);

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + Spacing.three }]}
      refreshControl={<RefreshControl refreshing={pulling} onRefresh={onPullToRefresh} />}>
      <View style={styles.column}>
        <Header offline={problem === 'offline'} syncedAt={syncedAt} />

        <View style={styles.dayRow}>
          <DayArrow
            direction="back"
            disabled={day <= range.first}
            onPress={() => goToDay(shiftDay(day, -1))}
          />
          <View style={styles.dayTitleBlock}>
            <Text style={styles.dayTitle} accessibilityRole="header">
              {formatDayTitle(day)}
            </Text>
            <Text style={styles.daySubtitle}>
              {summary(dayLessons.length, remaining, isToday)}
            </Text>
          </View>
          <DayArrow
            direction="forward"
            disabled={day >= range.last}
            onPress={() => goToDay(shiftDay(day, 1))}
          />
        </View>

        {!isToday ? (
          <Pressable accessibilityRole="button" onPress={() => setChosenDay(null)} style={styles.backToToday}>
            <Text style={styles.backToTodayText}>Back to today</Text>
          </Pressable>
        ) : null}

        <ProblemNotice problem={problem} syncedAt={syncedAt} onRetry={refresh} />

        {dayLessons.length > 0 ? (
          <View style={styles.list}>
            {dayLessons.map((lesson) => (
              <LessonRow key={lesson.id} lesson={lesson} now={now} isToday={isToday} />
            ))}
          </View>
        ) : (
          <Text style={styles.empty}>{emptyText(cacheRead, refreshing, lessons.length === 0)}</Text>
        )}
      </View>
    </ScrollView>
  );
}

function summary(total: number, remaining: number, isToday: boolean): string {
  if (total === 0) return 'No lessons';
  const noun = (n: number) => (n === 1 ? '1 lesson' : `${n} lessons`);
  return isToday ? `${remaining} of ${noun(total)} still to come` : noun(total);
}

function emptyText(cacheRead: boolean, refreshing: boolean, nothingCached: boolean): string {
  if (nothingCached && (!cacheRead || refreshing)) return 'Loading your timetable…';
  return 'No lessons on this day.';
}

function Header({ offline, syncedAt }: { offline: boolean; syncedAt: Date | null }) {
  return (
    <View style={styles.header}>
      <Text style={styles.appName}>TimeForYou</Text>
      {offline ? (
        <StatusChip kind="offline" />
      ) : syncedAt ? (
        <View style={styles.synced}>
          <View style={styles.syncedDot} />
          <Text style={styles.syncedText}>Updated {formatTime(syncedAt)}</Text>
        </View>
      ) : null}
    </View>
  );
}

function DayArrow({
  direction,
  disabled,
  onPress,
}: {
  direction: 'back' | 'forward';
  disabled: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={direction === 'back' ? 'Previous day' : 'Next day'}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      hitSlop={8}
      style={[styles.arrow, disabled && styles.arrowDisabled]}>
      <Ionicons
        name={direction === 'back' ? 'chevron-back' : 'chevron-forward'}
        size={22}
        color={Brand.navy}
      />
    </Pressable>
  );
}

function ProblemNotice({
  problem,
  syncedAt,
  onRetry,
}: {
  problem: TimetableProblem;
  syncedAt: Date | null;
  onRetry: () => void;
}) {
  if (!problem) return null;

  const text =
    problem === 'offline'
      ? `You're offline. Showing your last saved timetable${syncedAt ? ` (synced ${formatTime(syncedAt)})` : ''}.`
      : problem === 'session'
        ? 'Your session has expired. Link your account again to see your timetable.'
        : "Couldn't refresh your timetable. Showing what was saved.";

  return (
    <View style={styles.notice} accessibilityRole="alert">
      <Text style={styles.noticeText}>{text}</Text>
      {problem !== 'session' ? <Button label="Try again" variant="secondary" onPress={onRetry} /> : null}
    </View>
  );
}

function LessonRow({ lesson, now, isToday }: { lesson: Lesson; now: Date; isToday: boolean }) {
  const router = useRouter();
  // Only today has a "now": other days show every lesson as it is scheduled.
  const phase = isToday ? lessonPhase(lesson, now) : 'upcoming';
  const cancelled = lesson.status === 'cancelled';

  return (
    <View style={[styles.lessonRow, phase === 'past' && styles.past]}>
      {cancelled ? <StatusChip kind="cancelled" /> : phase === 'live' ? <StatusChip kind="live" /> : null}
      <LessonCard
        status={cardStatusFor(lesson, phase)}
        start={formatTime(lesson.start_ts)}
        end={formatTime(lesson.end_ts)}
        title={lesson.subject}
        room={lesson.room}
        teacher={lesson.lecturer}
        onPress={() => router.push(`/lesson/${encodeURIComponent(lesson.id)}`)}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  appName: { fontFamily: Font.textBold, fontSize: 20, color: Brand.navy },
  synced: { flexDirection: 'row', alignItems: 'center', gap: Spacing.one },
  syncedDot: { width: 8, height: 8, borderRadius: Radius.pill, backgroundColor: Status.onTime.strip },
  syncedText: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  dayRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  dayTitleBlock: { flex: 1, gap: Spacing.half },
  dayTitle: { fontFamily: Font.textBold, fontSize: 28, color: Brand.navy },
  daySubtitle: { fontFamily: Font.label, fontSize: 14, color: Colors.light.textSecondary },
  arrow: {
    width: 40,
    height: 40,
    borderRadius: Radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Brand.white,
    borderWidth: 1,
    borderColor: Brand.borderStrong,
  },
  arrowDisabled: { opacity: 0.35 },
  backToToday: { alignSelf: 'flex-start', paddingVertical: Spacing.one },
  backToTodayText: {
    fontFamily: Font.textSemiBold,
    fontSize: 14,
    color: Brand.navy,
    textDecorationLine: 'underline',
  },
  notice: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },
  list: { gap: Spacing.three },
  lessonRow: { gap: Spacing.two },
  past: { opacity: 0.55 },
  empty: {
    fontFamily: Font.text,
    fontSize: 16,
    color: Colors.light.textSecondary,
    textAlign: 'center',
    paddingVertical: Spacing.five,
  },
});
