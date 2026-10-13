import { Ionicons } from '@expo/vector-icons';
import Constants from 'expo-constants';
import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api, NetworkError, onUnauthorized, UnauthorizedError, type Profile } from '@/shared/api';
import { Button } from '@/shared/components/button';
import { usePushState } from '@/shared/push/push-state';
import type { PushState } from '@/shared/push/types';
import { changesStore, lessonCache } from '@/shared/storage';
import { sortLessonsByStart } from '@/shared/storage/sort-lessons';
import { announceSynced } from '@/shared/sync/synced';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';

import { Row, Section, ToggleRow } from './settings-rows';

const NOTIFICATION_LABEL: Record<PushState, string> = {
  checking: '',
  registered: 'On',
  denied: 'Off',
  default: 'Not set up',
  failed: 'Error',
  unsupported: 'Unavailable',
};

type SyncTone = 'ok' | 'problem' | 'busy';

/** The "Sync status" pill on the profile card, by how the last sync went. */
const SYNC_PILL: Record<SyncTone, { bg: string; border: string; fg: string; dot: string }> = {
  ok: { bg: Status.onTime.bg, border: Status.onTime.border, fg: Status.onTime.text, dot: Status.onTime.strip },
  problem: {
    bg: Status.cancelled.bg,
    border: Status.cancelled.border,
    fg: Status.cancelled.text,
    dot: Status.cancelled.strip,
  },
  busy: {
    bg: Colors.light.backgroundElement,
    border: Colors.light.backgroundSelected,
    fg: Colors.light.textSecondary,
    dot: Colors.light.textSecondary,
  },
};

/** "09:41". */
function formatClock(date: Date): string {
  return date.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
}

/** "Wed 14 Oct 09:41". */
function formatSynced(date: Date): string {
  const day = date.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
  return `${day} ${formatClock(date)}`;
}

/** "Nicholas Bence" -> "NB". */
function initials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((word) => word.charAt(0).toUpperCase())
    .join('');
}

/**
 * The Settings tab (#108). Rows whose feature is not built yet are shown disabled with a "Soon" tag
 * and invent nothing. Unlinking asks first, then removes the device on the server and signs out.
 */
export function SettingsScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const push = usePushState();
  const [syncing, setSyncing] = useState(true);
  const [syncedAt, setSyncedAt] = useState<Date | null>(null);
  const [problem, setProblem] = useState<'offline' | 'failed' | null>(null);
  // The student's name from WebUntis (#113). Null until loaded, or if it can't be.
  const [profile, setProfile] = useState<Profile | null>(null);
  // "Change alerts" (#110): null until the server says. Saved on the user, so it covers every device.
  const [alerts, setAlerts] = useState<boolean | null>(null);
  const [savingAlerts, setSavingAlerts] = useState(false);
  const [alertsError, setAlertsError] = useState<string | null>(null);
  const mounted = useRef(true);
  // Set the moment a 401 or an unlink signs the student out, so a fetch still in flight cannot save
  // their data back after the wipe.
  const signedOut = useRef(false);

  // Fetches the timetable and the changes and saves both, as the other screens do. State is only
  // set after the requests settle. Never rejects. Only a tap on Sync now tells the other screens
  // to refetch (`announce`); the sync on opening Settings stays quiet so it does not triple the requests.
  const sync = useCallback(
    (announce: boolean) =>
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
          if (announce) announceSynced();
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
    void sync(false);
    return () => {
      mounted.current = false;
      stopListening();
    };
  }, [sync]);

  // On every visit, not just the first mount: the worker fills the name in on its own schedule, so a
  // student who just linked sees it on their next look at Settings. A failure leaves the card as it
  // was; the rest of Settings still works.
  useFocusEffect(
    useCallback(() => {
      let cancelled = false;
      api.getProfile().then(
        (loaded) => !cancelled && setProfile(loaded),
        () => undefined,
      );
      return () => {
        cancelled = true;
      };
    }, []),
  );

  useEffect(() => {
    let cancelled = false;
    api.getSettings().then(
      (settings) => !cancelled && setAlerts(settings.change_alerts_enabled),
      (error: unknown) => {
        // A 401 has already signed the student out.
        if (cancelled || error instanceof UnauthorizedError) return;
        setAlertsError(
          error instanceof NetworkError
            ? "You're offline, so change alerts can't be changed right now."
            : "Couldn't load your change alerts setting.",
        );
      },
    );
    return () => {
      cancelled = true;
    };
  }, []);

  // Shows the new position at once, then saves it; if saving fails it flips back and says why.
  async function toggleAlerts(next: boolean) {
    const before = alerts;
    setAlerts(next);
    setSavingAlerts(true);
    setAlertsError(null);
    try {
      const saved = await api.updateSettings({ change_alerts_enabled: next });
      if (mounted.current) setAlerts(saved.change_alerts_enabled);
    } catch (error) {
      if (!mounted.current || error instanceof UnauthorizedError) return;
      setAlerts(before);
      setAlertsError(
        error instanceof NetworkError
          ? "You're offline. Your change alerts setting wasn't changed."
          : "Couldn't save your change alerts setting. Try again.",
      );
    } finally {
      if (mounted.current) setSavingAlerts(false);
    }
  }

  function syncNow() {
    setSyncing(true);
    void sync(true);
  }

  const syncChip = problem === 'offline' ? 'Offline' : problem ? "Couldn't sync" : syncedAt ? 'Synced' : '';
  const syncTone: SyncTone = problem ? 'problem' : syncing || !syncedAt ? 'busy' : 'ok';
  const syncLabel =
    problem === 'offline'
      ? 'Offline'
      : problem
        ? "Couldn't sync"
        : syncing || !syncedAt
          ? 'Syncing…'
          : `Synced ${formatClock(syncedAt)}`;

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + Spacing.three }]}>
      <View style={styles.column}>
        <Text style={styles.title} accessibilityRole="header">
          Settings
        </Text>

        <ProfileCard profile={profile} syncTone={syncTone} syncLabel={syncLabel} />

        <Section label="Notifications">
          <ToggleRow
            icon="notifications-outline"
            title="Change alerts"
            subtitle={
              alerts === false
                ? 'Off on all your devices. Changes still show in the app.'
                : 'Class relocations & cancellations'
            }
            value={alerts}
            onChange={toggleAlerts}
            disabled={savingAlerts}
          />
          {alertsError ? (
            <Text style={styles.alertsError} accessibilityRole="alert">
              {alertsError}
            </Text>
          ) : null}
          <Row icon="checkmark-circle-outline" title="Notification status" value={NOTIFICATION_LABEL[push]} />
        </Section>

        <Section label="Calendar">
          <Row
            icon="calendar-outline"
            title="Calendar subscription"
            subtitle="Sync feeds with iOS & Google Calendar"
            arrow
            onPress={() => router.push('/calendar')}
          />
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
          <Row icon="help-circle-outline" title="Help and FAQ" arrow onPress={() => router.push('/help')} />
          <Row icon="information-circle-outline" title="Version" value={Constants.expoConfig?.version ?? ''} />
        </Section>

        <UnlinkDevice />
      </View>
    </ScrollView>
  );
}

/** Who is signed in (#113): name from WebUntis, and how the last sync went. */
function ProfileCard({
  profile,
  syncTone,
  syncLabel,
}: {
  profile: Profile | null;
  syncTone: SyncTone;
  syncLabel: string;
}) {
  const name = profile?.display_name ?? null;
  const pill = SYNC_PILL[syncTone];
  return (
    <View style={styles.profile}>
      <View style={styles.profileTop}>
        <View style={styles.avatar}>
          {name ? (
            <Text style={styles.avatarText}>{initials(name)}</Text>
          ) : (
            <Ionicons name="person-outline" size={26} color={Brand.white} />
          )}
        </View>
        <View style={styles.profileText}>
          <Text style={styles.profileName} numberOfLines={1}>
            {name ?? 'Your profile'}
          </Text>
          {!name && (
            <Text style={styles.profileClass} numberOfLines={2}>
              Your name appears after the first sync.
            </Text>
          )}
        </View>
      </View>
      <View style={styles.divider} />
      <View style={styles.syncRow}>
        <Text style={styles.syncLabel}>Sync status</Text>
        <View style={[styles.pill, { backgroundColor: pill.bg, borderColor: pill.border }]}>
          <View style={[styles.pillDot, { backgroundColor: pill.dot }]} />
          <Text style={[styles.pillText, { color: pill.fg }]}>{syncLabel}</Text>
        </View>
      </View>
    </View>
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

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  title: { fontFamily: Font.textBold, fontSize: 24, color: Brand.navy },

  profile: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  profileTop: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  avatar: {
    width: 56,
    height: 56,
    borderRadius: Radius.pill,
    backgroundColor: Brand.navy,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarText: { fontFamily: Font.textBold, fontSize: 20, color: Brand.white },
  profileText: { flex: 1, gap: Spacing.half },
  profileName: { fontFamily: Font.textBold, fontSize: 20, color: Brand.navy },
  profileClass: { fontFamily: Font.label, fontSize: 14, color: Colors.light.textSecondary },
  divider: { height: 1, backgroundColor: Brand.border },
  syncRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: Spacing.two },
  syncLabel: { fontFamily: Font.text, fontSize: 14, color: Colors.light.textSecondary },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    paddingHorizontal: Spacing.three,
    paddingVertical: Spacing.one,
    borderRadius: Radius.pill,
    borderWidth: 1,
  },
  pillDot: { width: 8, height: 8, borderRadius: Radius.pill },
  pillText: { fontFamily: Font.labelSemiBold, fontSize: 13, fontVariant: ['tabular-nums'] },

  alertsError: {
    fontFamily: Font.textSemiBold,
    fontSize: 13,
    color: Status.cancelled.text,
    paddingHorizontal: Spacing.three,
    paddingBottom: Spacing.three,
  },

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