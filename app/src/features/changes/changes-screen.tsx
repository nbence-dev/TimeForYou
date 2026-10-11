import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Button } from '@/shared/components/button';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';

import { ChangeCard } from './change-card';
import { countByType, formatTime, sortChanges, type ShownChangeType } from './change-format';
import { useChanges, type ChangesProblem } from './use-changes';

/**
 * Where a notification tap lands (#71): every recent change, newest first, so a grouped alert
 * expands into the full picture. Reached at `/changes`, the Changes tab.
 */
export function ChangesScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { changes, lessons, loaded, refreshing, problem, checkedAt, refresh } = useChanges();
  const [pulling, setPulling] = useState(false);

  // Load each time the tab is shown: a notification tap can land here while it is already open.
  useFocusEffect(
    useCallback(() => {
      void refresh();
    }, [refresh]),
  );

  const shown = useMemo(() => sortChanges(changes), [changes]);
  const counts = useMemo(() => countByType(shown), [shown]);

  async function onPullToRefresh() {
    setPulling(true);
    try {
      await refresh();
    } finally {
      setPulling(false);
    }
  }

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + Spacing.three }]}
      refreshControl={<RefreshControl refreshing={pulling} onRefresh={onPullToRefresh} />}>
      <View style={styles.column}>
        <Header />

        {problem ? <ProblemNotice problem={problem} onRetry={refresh} /> : null}

        {!loaded ? (
          <Text style={styles.muted}>Loading changes…</Text>
        ) : shown.length === 0 ? (
          problem ? null : (
            <EmptyState checkedAt={checkedAt} onViewTimetable={() => router.navigate('/timetable')} />
          )
        ) : (
          <>
            <Summary
              total={shown.length}
              counts={counts}
              checkedAt={checkedAt}
              refreshing={refreshing}
              onRefresh={refresh}
            />
            <View style={styles.list}>
              {shown.map((change) => (
                <ChangeCard key={change.id} change={change} lesson={lessons.get(change.lesson_id)} />
              ))}
            </View>
          </>
        )}
      </View>
    </ScrollView>
  );
}

function Header() {
  return (
    <View style={styles.header}>
      <View style={styles.headerIcon}>
        <Ionicons name="calendar-outline" size={22} color={Brand.navy} />
      </View>
      <Text style={styles.headerTitle} accessibilityRole="header">
        Changes
      </Text>
    </View>
  );
}

const PILLS: { type: ShownChangeType; label: string; colors: { strip: string; bg: string; text: string; border: string } }[] = [
  { type: 'cancelled', label: 'cancelled', colors: Status.cancelled },
  { type: 'room', label: 'room', colors: Status.changed },
  { type: 'time', label: 'time', colors: Status.changed },
];

function Summary({
  total,
  counts,
  checkedAt,
  refreshing,
  onRefresh,
}: {
  total: number;
  counts: Record<ShownChangeType, number>;
  checkedAt: Date | null;
  refreshing: boolean;
  onRefresh: () => void;
}) {
  return (
    <View style={styles.summary}>
      <View style={styles.summaryTop}>
        <View style={styles.flex}>
          <Text style={styles.eyebrow}>
            SCHEDULE ALERT{checkedAt ? ` · Updated ${formatTime(checkedAt)}` : ''}
          </Text>
          <Text style={styles.summaryTitle}>
            {total === 1 ? '1 change' : `${total} changes`} in your timetable
          </Text>
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Refresh"
          accessibilityState={{ disabled: refreshing }}
          disabled={refreshing}
          onPress={onRefresh}
          hitSlop={8}
          style={[styles.refresh, refreshing && styles.refreshBusy]}>
          <Ionicons name="sync-outline" size={20} color={Brand.navy} />
        </Pressable>
      </View>
      <View style={styles.pills}>
        {PILLS.map(({ type, label, colors }) =>
          counts[type] > 0 ? (
            <View key={type} style={[styles.pill, { backgroundColor: colors.bg, borderColor: colors.border }]}>
              <View style={[styles.pillDot, { backgroundColor: colors.strip }]} />
              <Text style={[styles.pillText, { color: colors.text }]}>
                {counts[type]} {label}
              </Text>
            </View>
          ) : null,
        )}
      </View>
    </View>
  );
}

function EmptyState({ checkedAt, onViewTimetable }: { checkedAt: Date | null; onViewTimetable: () => void }) {
  return (
    <View style={styles.empty}>
      <View style={styles.emptyIcon}>
        <Ionicons name="checkmark" size={44} color={Status.onTime.border} />
      </View>
      <Text style={styles.emptyTitle}>You&apos;re all caught up</Text>
      <Text style={styles.emptyBody}>
        When a lesson is cancelled, moved or changes room, it will show up here.
      </Text>
      <Button label="View timetable" variant="secondary" onPress={onViewTimetable} />
      {checkedAt ? <Text style={styles.muted}>Last checked {formatTime(checkedAt)}</Text> : null}
    </View>
  );
}

function ProblemNotice({
  problem,
  onRetry,
}: {
  problem: Exclude<ChangesProblem, null>;
  onRetry: () => void;
}) {
  const text =
    problem === 'offline'
      ? "You're offline. Your changes will show here once you're back online."
      : problem === 'session'
        ? 'Your session has expired. Link your account again to see your changes.'
        : "Couldn't load your changes. Try again in a moment.";

  return (
    <View style={styles.notice} accessibilityRole="alert">
      <Text style={styles.noticeText}>{text}</Text>
      {problem !== 'session' ? <Button label="Try again" variant="secondary" onPress={onRetry} /> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  flex: { flex: 1, gap: Spacing.one },

  header: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  headerIcon: {
    width: 44,
    height: 44,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
    alignItems: 'center',
    justifyContent: 'center',
  },
  headerTitle: { fontFamily: Font.textBold, fontSize: 24, color: Brand.navy },

  summary: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  summaryTop: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.three },
  eyebrow: { fontFamily: Font.labelSemiBold, fontSize: 12, letterSpacing: 1, color: Status.changed.text },
  summaryTitle: { fontFamily: Font.textBold, fontSize: 20, color: Brand.navy },
  refresh: {
    width: 44,
    height: 44,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
    alignItems: 'center',
    justifyContent: 'center',
  },
  refreshBusy: { opacity: 0.5 },
  pills: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    paddingHorizontal: Spacing.three,
    paddingVertical: Spacing.one,
    borderRadius: Radius.base,
    borderWidth: 1,
  },
  pillDot: { width: 8, height: 8, borderRadius: Radius.pill },
  pillText: { fontFamily: Font.labelSemiBold, fontSize: 13 },

  list: { gap: Spacing.three },

  empty: { alignItems: 'center', gap: Spacing.three, paddingVertical: Spacing.five, paddingHorizontal: Spacing.three },
  emptyIcon: {
    width: 96,
    height: 96,
    borderRadius: Radius.pill,
    backgroundColor: Brand.navy,
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyTitle: { fontFamily: Font.textBold, fontSize: 20, color: Brand.navy, textAlign: 'center' },
  emptyBody: {
    fontFamily: Font.text,
    fontSize: 15,
    lineHeight: 22,
    color: Colors.light.textSecondary,
    textAlign: 'center',
  },
  muted: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary, textAlign: 'center' },

  notice: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },
});