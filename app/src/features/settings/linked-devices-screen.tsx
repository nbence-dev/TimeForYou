import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState, type ComponentProps } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api, NetworkError, UnauthorizedError, type LinkedDevice } from '@/shared/api';
import { Banner } from '@/shared/components/banner';
import { Button } from '@/shared/components/button';
import { ScreenHeader } from '@/shared/components/screen-header';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

/** "Last active 5 min ago", from the server's last-seen time. */
function lastActive(iso: string, now: Date): string {
  const ms = now.getTime() - new Date(iso).getTime();
  if (!Number.isFinite(ms)) return '';
  const minutes = Math.floor(ms / 60000);
  if (minutes < 1) return 'Last active just now';
  if (minutes < 60) return `Last active ${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `Last active ${hours} h ago`;
  const days = Math.floor(hours / 24);
  return `Last active ${days} ${days === 1 ? 'day' : 'days'} ago`;
}

/** What we know of a device is its platform: there is no device name (#111). */
function platformInfo(platform: LinkedDevice['platform']): { title: string; icon: IconName } {
  if (platform === 'android_fcm') return { title: 'Android app', icon: 'logo-android' };
  if (platform === 'web_push') return { title: 'Web app', icon: 'globe-outline' };
  return { title: 'Device', icon: 'phone-portrait-outline' };
}

/**
 * The student's own linked devices (#111): the platform, whether notifications are on, when it was
 * last active, and a "This device" marker. No names: the platform label is all the API knows.
 */
export function LinkedDevicesScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const [devices, setDevices] = useState<LinkedDevice[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [problem, setProblem] = useState<'offline' | 'failed' | null>(null);
  const mounted = useRef(true);
  // A 401 signed the student out: keep showing "Loading" instead of a misleading error.
  const signedOut = useRef(false);

  // State is only set after the request settles. Never rejects.
  const load = useCallback(
    () =>
      api
        .listDevices()
        .then((list) => {
          if (!mounted.current) return;
          setDevices(list);
          setProblem(null);
        })
        .catch((error: unknown) => {
          // A 401 has already signed the student out; the app is on its way to the scan screen.
          if (error instanceof UnauthorizedError) {
            signedOut.current = true;
            return;
          }
          if (mounted.current) setProblem(error instanceof NetworkError ? 'offline' : 'failed');
        })
        .finally(() => {
          if (mounted.current && !signedOut.current) setLoading(false);
        }),
    [],
  );

  useEffect(() => {
    mounted.current = true;
    void load();
    return () => {
      mounted.current = false;
    };
  }, [load]);

  function retry() {
    setLoading(true);
    void load();
  }

  const close = () => (router.canGoBack() ? router.back() : router.replace('/settings'));
  const now = new Date();

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + Spacing.four }]}>
      <View style={styles.column}>
        <ScreenHeader title="Linked devices" onBack={close} />

        <View style={styles.intro}>
          <Ionicons name="sync-outline" size={22} color={Brand.navy} />
          <Text style={styles.introText}>Notes and settings are shared across devices linked to the same WebUntis account.</Text>
        </View>

        <View style={styles.listHeader}>
          <Text style={styles.label}>YOUR DEVICES</Text>
          {devices ? <Text style={styles.count}>{devices.length === 1 ? '1 device' : `${devices.length} devices`}</Text> : null}
        </View>

        {loading ? (
          <Text style={styles.body}>Loading your devices…</Text>
        ) : devices ? (
          devices.map((device) => <DeviceCard key={device.id} device={device} now={now} />)
        ) : (
          <Banner>
            <Text style={styles.noticeText}>
              {problem === 'offline'
                ? "You're offline. Connect to the internet to see your devices."
                : "Couldn't load your devices. Try again in a moment."}
            </Text>
            <Button label="Try again" variant="secondary" onPress={retry} />
          </Banner>
        )}

        <View style={styles.link}>
          <Text style={styles.linkTitle}>Link another device</Text>
          <Text style={styles.body}>Open TimeForYou on the new device and scan the same WebUntis QR code.</Text>
        </View>
      </View>
    </ScrollView>
  );
}

function DeviceCard({ device, now }: { device: LinkedDevice; now: Date }) {
  const { title, icon } = platformInfo(device.platform);
  return (
    <View style={[styles.card, device.is_current && styles.cardCurrent]}>
      <View style={[styles.strip, device.is_current && styles.stripCurrent]} />
      <View style={styles.cardBody}>
        <View style={styles.cardTop}>
          <View style={styles.deviceIcon}>
            <Ionicons name={icon} size={22} color={Brand.navy} />
          </View>
          <View style={styles.cardText}>
            <View style={styles.titleRow}>
              <Text style={styles.deviceTitle}>{title}</Text>
              {device.is_current ? (
                <View style={styles.thisDevice}>
                  <Text style={styles.thisDeviceText}>This device</Text>
                </View>
              ) : null}
            </View>
            <View style={styles.metaRow}>
              <Ionicons
                name={device.notifications_on ? 'notifications' : 'notifications-off-outline'}
                size={14}
                color={device.notifications_on ? Status.onTime.text : Colors.light.textSecondary}
              />
              <Text style={[styles.meta, device.notifications_on && styles.metaOn]}>
                {device.notifications_on ? 'Notifications on' : 'Notifications off'}
              </Text>
            </View>
            {/* The device asking is active now by definition; the phone's clock may differ from the server's. */}
            <Text style={styles.meta}>
              {device.is_current ? 'Last active just now' : lastActive(device.last_seen_at, now)}
            </Text>
          </View>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },

  intro: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    backgroundColor: Colors.light.backgroundElement,
  },
  introText: { flex: 1, fontFamily: Font.text, fontSize: 14, lineHeight: 20, color: Brand.navy },

  listHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  label: { fontFamily: Font.labelSemiBold, fontSize: 12, letterSpacing: 1, color: Colors.light.textSecondary },
  count: { fontFamily: Font.labelSemiBold, fontSize: 12, color: Brand.navy },
  body: { fontFamily: Font.text, fontSize: 14, lineHeight: 20, color: Colors.light.text },

  card: {
    flexDirection: 'row',
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
    overflow: 'hidden',
  },
  cardCurrent: { borderColor: Status.onTime.border },
  strip: { width: 4, backgroundColor: Brand.borderStrong },
  stripCurrent: { backgroundColor: Status.onTime.strip },
  cardBody: { flex: 1, padding: Spacing.three },
  cardTop: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  deviceIcon: {
    width: 44,
    height: 44,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
    alignItems: 'center',
    justifyContent: 'center',
  },
  cardText: { flex: 1, gap: Spacing.one },
  titleRow: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: Spacing.two },
  deviceTitle: { fontFamily: Font.textBold, fontSize: 17, color: Brand.navy },
  thisDevice: {
    paddingHorizontal: Spacing.two,
    paddingVertical: Spacing.half,
    borderRadius: Radius.pill,
    borderWidth: 1,
    borderColor: Status.onTime.border,
    backgroundColor: Status.onTime.bg,
  },
  thisDeviceText: { fontFamily: Font.labelSemiBold, fontSize: 12, color: Status.onTime.text },
  metaRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.one },
  meta: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  metaOn: { color: Status.onTime.text },

  notice: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },

  link: {
    gap: Spacing.two,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: Brand.orange,
    backgroundColor: Brand.white,
  },
  linkTitle: { fontFamily: Font.textBold, fontSize: 17, color: Brand.navy },
});
