import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

import { Banner } from '@/shared/components/banner';
import { Button } from '@/shared/components/button';
import { Card } from '@/shared/components/card';
import { EmptyState } from '@/shared/components/empty-state';
import { ScreenHeader } from '@/shared/components/screen-header';
import { LoadingSkeleton } from '@/shared/components/skeleton';
import { StatusChip } from '@/shared/components/status-chip';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status, TitleSize } from '@/shared/theme/theme';

import { ChangeCard } from './change-card';
import { splitByArchive } from './change-archive';
import { countByType, formatTime, sortChanges, type ShownChangeType } from './change-format';
import { useArchive } from './use-archive';
import { useChanges, type ChangesProblem } from './use-changes';

/**
 * Where a notification tap lands (#71): the recent changes, newest first, so a grouped alert
 * expands into the full picture. Reached at `/changes`, the Changes tab. "Mark all as read" moves
 * what is shown into the Archive, kept on this device (#138); only new changes are listed by default.
 */
export function ChangesScreen() {
  const router = useRouter();
  const { changes, lessons, loaded, refreshing, problem, checkedAt, refresh } = useChanges();
  const { archived, ready, archiveAll } = useArchive();
  const [view, setView] = useState<'new' | 'archive'>('new');
  const [pulling, setPulling] = useState(false);

  // Load each time the tab is shown: a notification tap can land here while it is already open.
  // It also lands on New, so a tap never opens on the Archive the student left it on.
  useFocusEffect(
    useCallback(() => {
      setView('new');
      void refresh();
    }, [refresh]),
  );

  const sorted = useMemo(() => sortChanges(changes), [changes]);
  const { fresh: shown, archived: archivedChanges } = useMemo(
    () => splitByArchive(sorted, archived),
    [sorted, archived],
  );
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
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={pulling} onRefresh={onPullToRefresh} />}>
      <View style={styles.column}>
        <ScreenHeader title="Changes" right={problem === 'offline' ? <StatusChip kind="offline" /> : undefined} />

        <ViewToggle view={view} onChange={setView} newCount={shown.length} archivedCount={archivedChanges.length} />

        {problem ? <ProblemNotice problem={problem} onRetry={refresh} /> : null}

        {!loaded || !ready ? (
          <LoadingSkeleton label="Loading changes" onRetry={refresh} />
        ) : view === 'archive' ? (
          archivedChanges.length === 0 ? (
            problem ? null : <ArchiveEmpty />
          ) : (
            <View style={styles.list}>
              {archivedChanges.map((change) => (
                <ChangeCard key={change.id} change={change} lesson={lessons.get(change.lesson_id)} />
              ))}
            </View>
          )
        ) : shown.length === 0 ? (
          problem ? null : (
            <CaughtUp checkedAt={checkedAt} onViewTimetable={() => router.navigate('/timetable')} />
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
            <Button label="Mark all as read" variant="secondary" onPress={() => archiveAll(shown)} />
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

/** New and Archive: two views of the same screen, each with how many changes it holds. */
function ViewToggle({
  view,
  onChange,
  newCount,
  archivedCount,
}: {
  view: 'new' | 'archive';
  onChange: (view: 'new' | 'archive') => void;
  newCount: number;
  archivedCount: number;
}) {
  const tabs = [
    { key: 'new' as const, label: `New (${newCount})` },
    { key: 'archive' as const, label: `Archive (${archivedCount})` },
  ];
  return (
    <View style={styles.toggle} accessibilityRole="tablist">
      {tabs.map(({ key, label }) => {
        const active = view === key;
        return (
          <Pressable
            key={key}
            accessibilityRole="tab"
            accessibilityState={{ selected: active }}
            onPress={() => onChange(key)}
            style={[styles.tab, active && styles.tabActive]}>
            <Text style={[styles.tabText, active && styles.tabTextActive]}>{label}</Text>
          </Pressable>
        );
      })}
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
    <Card>
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
    </Card>
  );
}

function CaughtUp({ checkedAt, onViewTimetable }: { checkedAt: Date | null; onViewTimetable: () => void }) {
  return (
    <EmptyState
      icon="checkmark-circle-outline"
      title="You're all caught up"
      text="When a lesson is cancelled, moved or changes room, it will show up here.">
      <Button label="View timetable" variant="secondary" onPress={onViewTimetable} />
      {checkedAt ? <Text style={styles.muted}>Last checked {formatTime(checkedAt)}</Text> : null}
    </EmptyState>
  );
}

function ArchiveEmpty() {
  return (
    <EmptyState icon="archive-outline" title="Nothing archived yet" text="Changes you mark as read are kept here." />
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
    <Banner>
      <Text style={styles.noticeText}>{text}</Text>
      {problem !== 'session' ? <Button label="Try again" variant="secondary" onPress={onRetry} /> : null}
    </Banner>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  flex: { flex: 1, gap: Spacing.one },

  toggle: {
    flexDirection: 'row',
    padding: Spacing.one,
    gap: Spacing.one,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
  },
  tab: { flex: 1, minHeight: 40, alignItems: 'center', justifyContent: 'center', borderRadius: Radius.base },
  tabActive: { backgroundColor: Brand.white },
  tabText: { fontFamily: Font.textSemiBold, fontSize: 14, color: Colors.light.textSecondary },
  tabTextActive: { color: Brand.navy },

  summaryTop: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.three },
  eyebrow: { fontFamily: Font.labelSemiBold, fontSize: 12, letterSpacing: 1, color: Status.changed.text },
  summaryTitle: { fontFamily: Font.textBold, fontSize: TitleSize.section, color: Brand.navy },
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

  muted: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary, textAlign: 'center' },

  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },
});