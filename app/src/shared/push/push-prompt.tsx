import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Button } from '@/shared/components/button';
import { Font } from '@/shared/theme/fonts';
import { Brand, Radius, Spacing } from '@/shared/theme/theme';

import { enablePush, syncPush, type PushState } from './index';

/**
 * Registers for push on launch and, while permission is still open or refused, shows a strip that
 * says so. Renders nothing where push is unavailable or already working.
 */
export function PushPrompt() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const [state, setState] = useState<PushState>('checking');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    syncPush().then(
      (s) => !cancelled && setState(s),
      () => !cancelled && setState('failed'),
    );
    return () => {
      cancelled = true;
    };
  }, []);

  // The service worker asks an already-open tab to go to Changes when a notification is tapped.
  useEffect(() => {
    if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) return;
    const onMessage = (e: MessageEvent) => {
      if (e.data?.type === 'open-changes') router.navigate('/changes');
    };
    navigator.serviceWorker.addEventListener('message', onMessage);
    return () => navigator.serviceWorker.removeEventListener('message', onMessage);
  }, [router]);

  if (state === 'default') {
    return (
      <View style={[styles.strip, { marginTop: insets.top + Spacing.three }]}>
        <Text style={styles.text}>Get a notification when a lesson changes.</Text>
        <Button
          label="Turn on notifications"
          variant="accent"
          disabled={busy}
          onPress={async () => {
            setBusy(true);
            try {
              setState(await enablePush());
            } catch {
              setState('failed');
            } finally {
              setBusy(false);
            }
          }}
        />
      </View>
    );
  }
  if (state === 'denied') {
    return (
      <View style={[styles.strip, { marginTop: insets.top + Spacing.three }]}>
        <Text style={styles.text}>
          Notifications are off. Turn them on for TimeForYou in your phone&apos;s Settings to hear
          about changes.
        </Text>
      </View>
    );
  }
  if (state === 'failed') {
    return (
      <View style={[styles.strip, { marginTop: insets.top + Spacing.three }]}>
        <Text style={styles.text}>
          Couldn&apos;t set up notifications. We&apos;ll try again next time you open the app.
        </Text>
      </View>
    );
  }
  return null;
}

const styles = StyleSheet.create({
  strip: {
    margin: Spacing.three,
    padding: Spacing.three,
    gap: Spacing.two,
    backgroundColor: Brand.white,
    borderWidth: 1,
    borderColor: Brand.border,
    borderRadius: Radius.base,
  },
  text: { fontFamily: Font.text, fontSize: 14, color: Brand.navy },
});
