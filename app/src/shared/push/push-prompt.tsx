import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useEffect, useState, type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Button } from '@/shared/components/button';
import { Font } from '@/shared/theme/fonts';
import { Brand, Radius, Spacing } from '@/shared/theme/theme';

import { enablePush, onNotificationTapped, syncPush } from './index';
import { takeOpenChangesNote } from './notification-intent';
import { setPushState, usePushState } from './push-state';

/**
 * Registers for push on launch and, while permission is still open or refused, shows a strip that
 * says so. Renders nothing where push is unavailable or already working.
 */
export function PushPrompt() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const state = usePushState();
  const [busy, setBusy] = useState(false);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    syncPush().then(
      (s) => !cancelled && setPushState(s),
      () => !cancelled && setPushState('failed'),
    );
    return () => {
      cancelled = true;
    };
  }, []);

  // Native: a tapped notification opens Changes, whether the app was closed or already running.
  useEffect(() => onNotificationTapped(() => router.navigate('/changes')), [router]);

  // The service worker asks an already-open tab to go to Changes when a notification is tapped.
  useEffect(() => {
    if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) return;
    const onMessage = (e: MessageEvent) => {
      if (e.data?.type === 'open-changes') router.navigate('/changes');
    };
    navigator.serviceWorker.addEventListener('message', onMessage);
    return () => navigator.serviceWorker.removeEventListener('message', onMessage);
  }, [router]);

  // iOS ignores the URL a notification tap asks for, so the worker leaves a note instead; pick it up
  // on launch and whenever the app returns to the foreground.
  useEffect(() => {
    if (typeof document === 'undefined') return;
    const check = () => {
      takeOpenChangesNote().then((tapped) => tapped && router.navigate('/changes'));
    };
    check();
    const onVisible = () => document.visibilityState === 'visible' && check();
    document.addEventListener('visibilitychange', onVisible);
    window.addEventListener('pageshow', check);
    window.addEventListener('focus', check);
    return () => {
      document.removeEventListener('visibilitychange', onVisible);
      window.removeEventListener('pageshow', check);
      window.removeEventListener('focus', check);
    };
  }, [router]);

  // Closing a strip hides it until the next launch, which re-checks the state.
  if (dismissed) return null;
  const dismiss = () => setDismissed(true);
  const top = insets.top + Spacing.three;

  if (state === 'default') {
    return (
      <Strip marginTop={top} onDismiss={dismiss}>
        <Text style={styles.text}>Get a notification when a lesson changes.</Text>
        <Button
          label="Turn on notifications"
          variant="accent"
          disabled={busy}
          onPress={async () => {
            setBusy(true);
            try {
              setPushState(await enablePush());
            } catch {
              setPushState('failed');
            } finally {
              setBusy(false);
            }
          }}
        />
      </Strip>
    );
  }
  if (state === 'denied') {
    return (
      <Strip marginTop={top} onDismiss={dismiss}>
        <Text style={styles.text}>
          Notifications are off. Turn them on for TimeForYou in your phone&apos;s Settings to hear
          about changes.
        </Text>
      </Strip>
    );
  }
  if (state === 'failed') {
    return (
      <Strip marginTop={top} onDismiss={dismiss}>
        <Text style={styles.text}>
          Couldn&apos;t set up notifications. We&apos;ll try again next time you open the app.
        </Text>
      </Strip>
    );
  }
  return null;
}

/** The strip's frame: its content, and a close button in the corner (banners are dismissable). */
function Strip({
  marginTop,
  onDismiss,
  children,
}: {
  marginTop: number;
  onDismiss: () => void;
  children: ReactNode;
}) {
  return (
    <View style={[styles.strip, { marginTop }]}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Dismiss"
        hitSlop={8}
        onPress={onDismiss}
        style={styles.close}>
        <Ionicons name="close" size={18} color={Brand.navy} />
      </Pressable>
      {children}
    </View>
  );
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
  close: { position: 'absolute', top: Spacing.two, right: Spacing.two, zIndex: 1, padding: Spacing.one },
});
