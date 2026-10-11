import { Ionicons } from '@expo/vector-icons';
import Constants from 'expo-constants';
import { useCallback, useEffect, useRef, useState, type ComponentProps, type ReactNode } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api, NetworkError, onUnauthorized, UnauthorizedError } from '@/shared/api';
import { Button } from '@/shared/components/button';
import { syncPush, type PushState } from '@/shared/push';
import { changesStore, lessonCache } from '@/shared/storage';
import { sortLessonsByStart } from '@/shared/storage/sort-lessons';
import { announceSynced } from '@/shared/sync/synced';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

const NOTIFICATION_LABEL: Record<PushState, string> = {
  checking: '',
  registered: 'On',
  denied: 'Off',
  default: 'Not set up',
  failed: 'Error',
  unsupported: 'Unavailable',
};

/** "Wed 14 Oct 09:41". */
function formatSynced(date: Date): string {
  const day = date.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
  const time = date.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  return `${day} ${time}`;
}

/**
 * The Settings tab (#108). Rows whose feature is not built yet are shown disabled with a "Soon" tag
 * and invent nothing. Unlinking asks first, then removes the device on the server and signs out.
 */
export function SettingsScreen() {
  const insets = useSafeAreaInsets();
  const [push, setPush] = useState<PushState>('checking');
  const [syncing, setSyncing] = useState(true);
  const [syncedAt, setSyncedAt] = useState<Date | null>(null);
  const [problem, setProblem] = useState<'offline' | 'failed' | null>(null);
  const mounted = useRef(true);
  // Set the moment a 401 or an unlink signs the student out, so a fetch still in flight cannot save
  // their data back after the wipe.
  const signedOut = useRef(false);

  // Fetches the timetable and the changes and saves both, as the other screens do. State is only
  // set after the requests settle. Never rejects.
  const sync = useCallback(
    () =>
      Promise.all([api.getTimetable(), api.getChanges()])
        .then(async ([lessons, changes]) => {
          if (signedOut.current) return;
          await Promise.allSettled([
            lessonCache.replaceLessons(sortLessonsByStart(lessons)),
            changesStore.writeCached(changes),
          ]);
          if (!mounted.current) return;
          setSyncedAt(new Date());
          setProblem(null);
          announceSynced();
        })
        .catch((error: unknown) => {
          // A 401 has already signed the student out; the app is on its way to the scan screen.
          if (error instanceof UnauthorizedError) return;
          if (mounted.current) setProblem(error instanceof NetworkError ? 'offline' : 'failed');
        })
        .finally(() => {
          if (mounted.current) setSyncing(false);
        }),
    [],
  );

  useEffect(() => {
    mounted.current = true;
    signedOut.current = false;
    const stopListening = onUnauthorized(() => {
      signedOut.current = true;
    });
    void sync();
    return () => {
      mounted.current = false;
      stopListening();
    };
  }, [sync]);

  useEffect(() => {
    let cancelled = false;
    // Never prompts: it only reports the state (and refreshes the token if already allowed).
    syncPush().then(
      (state) => !cancelled && setPush(state),
      () => !cancelled && setPush('failed'),
    );
    return () => {
      cancelled = true;
    };
  }, []);

  function syncNow() {
    setSyncing(true);
    void sync();
  }

  const syncChip = problem === 'offline' ? 'Offline' : problem ? "Couldn't sync" : syncedAt ? 'Synced' : '';

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + Spacing.three }]}>
      <View style={styles.column}>
        <Text style={styles.title} accessibilityRole="header">
          Settings
        </Text>

        <Section label="Notifications">
          <Row icon="notifications-outline" title="Change alerts" subtitle="Class relocations & cancellations" soon />
          <Row icon="checkmark-circle-outline" title="Notification status" value={NOTIFICATION_LABEL[push]} />
        </Section>

        <Section label="Calendar">
          <Row icon="calendar-outline" title="Calendar subscription" subtitle="Sync feeds with iOS & Google Calendar" soon />
        </Section>

        <Section label="Devices">
          <Row icon="phone-portrait-outline" title="Linked devices" soon />
        </Section>

        <Section label="Data">
          <Row
            icon="sync-outline"
            title="Sync now"
            value={syncChip}
            onPress={syncNow}
            disabled={syncing}
          />
          <Row icon="time-outline" title="Last synced" value={syncedAt ? formatSynced(syncedAt) : ''} />
        </Section>

        <Section label="About">
          <Row icon="help-circle-outline" title="Help and FAQ" soon />
          <Row icon="information-circle-outline" title="Version" value={Constants.expoConfig?.version ?? ''} />
        </Section>

        <UnlinkDevice />
      </View>
    </ScrollView>
  );
}

function UnlinkDevice() {
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function unlink() {
    setBusy(true);
    setError(null);
    try {
      // On success the app signs out and this screen goes away, so there is nothing to reset.
      await api.unlinkDevice();
    } catch (e) {
      // A 401 means the device was already gone: it is signed out and on its way to the scan screen.
      if (e instanceof UnauthorizedError) return;
      setError(
        e instanceof NetworkError
          ? "You're offline. Connect to the internet to unlink this device. You're still signed in."
          : "Couldn't unlink this device. You're still signed in. Try again in a moment.",
      );
      setBusy(false);
    }
  }

  if (!confirming) {
    return (
      <Button
        label="Unlink this device"
        variant="destructive"
        style={styles.unlink}
        onPress={() => setConfirming(true)}
      />
    );
  }

  return (
    <View style={styles.confirm}>
      <Text style={styles.confirmTitle}>Unlink this device?</Text>
      <Text style={styles.confirmBody}>
        You&apos;ll stop getting alerts here and will need to scan your QR code again to come back. Your
        notes stay saved.
      </Text>
      {error ? (
        <Text style={styles.error} accessibilityRole="alert">
          {error}
        </Text>
      ) : null}
      <Button label="Unlink" variant="destructive" disabled={busy} onPress={unlink} />
      <Button label="Cancel" variant="secondary" disabled={busy} onPress={() => setConfirming(false)} />
    </View>
  );
}

function Section({ label, children }: { label: string; children: ReactNode }) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionLabel}>{label.toUpperCase()}</Text>
      <View style={styles.card}>{children}</View>
    </View>
  );
}

function Row({
  icon,
  title,
  subtitle,
  value,
  soon,
  onPress,
  disabled,
}: {
  icon: IconName;
  title: string;
  subtitle?: string;
  value?: string;
  /** Not built yet: dimmed, tagged "Soon", and does nothing. */
  soon?: boolean;
  onPress?: () => void;
  disabled?: boolean;
}) {
  const inert = soon || disabled;
  return (
    <Pressable
      accessibilityRole={onPress && !soon ? 'button' : undefined}
      accessibilityState={{ disabled: !!inert }}
      accessibilityLabel={soon ? `${title}, coming soon` : undefined}
      disabled={inert || !onPress}
      onPress={onPress}
      style={[styles.row, soon && styles.rowSoon, disabled && !soon && styles.rowBusy]}>
      <View style={styles.rowIcon}>
        <Ionicons name={icon} size={20} color={Brand.navy} />
      </View>
      <View style={styles.rowText}>
        <Text style={styles.rowTitle}>{title}</Text>
        {subtitle ? <Text style={styles.rowSubtitle}>{subtitle}</Text> : null}
      </View>
      {soon ? (
        <View style={styles.soon}>
          <Text style={styles.soonText}>Soon</Text>
        </View>
      ) : value ? (
        <Text style={styles.rowValue}>{value}</Text>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  title: { fontFamily: Font.textBold, fontSize: 24, color: Brand.navy },

  section: { gap: Spacing.two },
  sectionLabel: { fontFamily: Font.labelSemiBold, fontSize: 12, letterSpacing: 1, color: Colors.light.textSecondary },
  card: {
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
    overflow: 'hidden',
  },

  row: {
    minHeight: 60,
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.three,
    padding: Spacing.three,
  },
  rowSoon: { opacity: 0.55 },
  rowBusy: { opacity: 0.6 },
  rowIcon: {
    width: 36,
    height: 36,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
    alignItems: 'center',
    justifyContent: 'center',
  },
  rowText: { flex: 1, gap: Spacing.half },
  rowTitle: { fontFamily: Font.textSemiBold, fontSize: 16, color: Brand.navy },
  rowSubtitle: { fontFamily: Font.text, fontSize: 13, color: Colors.light.textSecondary },
  rowValue: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  soon: {
    paddingHorizontal: Spacing.two,
    paddingVertical: Spacing.half,
    borderRadius: Radius.pill,
    borderWidth: 1,
    borderColor: Brand.borderStrong,
  },
  soonText: { fontFamily: Font.labelSemiBold, fontSize: 11, color: Colors.light.textSecondary },

  unlink: { backgroundColor: Status.cancelled.surface, borderColor: Status.cancelled.border },
  confirm: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Status.cancelled.border,
    backgroundColor: Status.cancelled.surface,
  },
  confirmTitle: { fontFamily: Font.textBold, fontSize: 18, color: Status.cancelled.text },
  confirmBody: { fontFamily: Font.text, fontSize: 14, lineHeight: 20, color: Colors.light.text },
  error: { fontFamily: Font.textSemiBold, fontSize: 14, color: Status.cancelled.text },
});
