import { useRouter } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

import { Banner } from '@/shared/components/banner';
import { Button } from '@/shared/components/button';
import { EmptyState } from '@/shared/components/empty-state';
import { LessonCard } from '@/shared/components/lesson-card';
import { NavArrow } from '@/shared/components/nav-arrow';
import { ScreenHeader } from '@/shared/components/screen-header';
import { StatusChip } from '@/shared/components/status-chip';
import { HomeScreenBanner } from '@/shared/pwa/home-screen-banner';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status, TitleSize } from '@/shared/theme/theme';
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
import { useChangeMarkers } from './change-markers-context';
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
  const { lessons, cacheRead, refreshing, problem, syncedAt, refresh } = useTimetable();
  const { reload: reloadChanges } = useChangeMarkers();
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
      await Promise.all([refresh(), reloadChanges()]);
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
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={pulling} onRefresh={onPullToRefresh} />}>
      <View style={styles.column}>
        <ScreenHeader title="Today" right={<SyncStatus offline={problem === 'offline'} syncedAt={syncedAt} />} />
        <HomeScreenBanner />

        <View style={styles.dayRow}>
          <NavArrow
            direction="back"
            label="Previous day"
            disabled={day <= range.first}
            onPress={() => goToDay(shiftDay(day, -1))}
          />
          <View style={styles.dayTitleBlock}>
            <Text style={styles.dayTitle}>{formatDayTitle(day)}</Text>
            <Text style={styles.daySubtitle}>
              {summary(dayLessons.length, remaining, isToday)}
            </Text>
          </View>
          <NavArrow
            direction="forward"
            label="Next day"
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
          <EmptyDay loading={lessons.length === 0 && (!cacheRead || refreshing)} />
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

function EmptyDay({ loading }: { loading: boolean }) {
  return loading ? (
    <EmptyState loading title="Loading your timetable" />
  ) : (
    <EmptyState icon="cafe-outline" title="No lessons on this day" />
  );
}

function SyncStatus({ offline, syncedAt }: { offline: boolean; syncedAt: Date | null }) {
  if (offline) return <StatusChip kind="offline" />;
  if (!syncedAt) return null;
  return (
    <View style={styles.synced}>
      <View style={styles.syncedDot} />
      <Text style={styles.syncedText}>Updated {formatTime(syncedAt)}</Text>
    </View>
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
    <Banner>
      <Text style={styles.noticeText}>{text}</Text>
      {problem !== 'session' ? <Button label="Try again" variant="secondary" onPress={onRetry} /> : null}
    </Banner>
  );
}

function LessonRow({ lesson, now, isToday }: { lesson: Lesson; now: Date; isToday: boolean }) {
  const router = useRouter();
  const { unseenLessonIds, markLessonSeen } = useChangeMarkers();
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
        group={lesson.group}
        mode={lesson.mode}
        hasNote={lesson.has_note}
        unseen={unseenLessonIds.has(lesson.id)}
        onPress={() => {
          markLessonSeen(lesson.id); // opening a lesson is seeing its changes
          router.push(`/lesson/${encodeURIComponent(lesson.id)}`);
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  synced: { flexDirection: 'row', alignItems: 'center', gap: Spacing.one },
  syncedDot: { width: 8, height: 8, borderRadius: Radius.pill, backgroundColor: Status.onTime.strip },
  syncedText: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  dayRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  dayTitleBlock: { flex: 1, alignItems: 'center', gap: Spacing.half },
  dayTitle: { fontFamily: Font.textBold, fontSize: TitleSize.section, color: Brand.navy },
  daySubtitle: { fontFamily: Font.label, fontSize: 14, color: Colors.light.textSecondary },
  backToToday: { alignSelf: 'center', paddingVertical: Spacing.one },
  backToTodayText: {
    fontFamily: Font.textSemiBold,
    fontSize: 14,
    color: Brand.navy,
    textDecorationLine: 'underline',
  },
  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },
  list: { gap: Spacing.three },
  lessonRow: { gap: Spacing.two },
  past: { opacity: 0.55 },
});
