import { CameraView, useCameraPermissions } from 'expo-camera';
import { useRef, useState } from 'react';
import { ActivityIndicator, Linking, Platform, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api, ApiError, NetworkError } from '@/shared/api';
import { Button } from '@/shared/components/button';
import { useSession } from '@/shared/session/session';
import { Font } from '@/shared/theme/fonts';
import { Brand, MaxContentWidth, Radius, Spacing } from '@/shared/theme/theme';

import { parseQrPayload } from './parse-qr';

const FIND_CODE_HINT = 'Find it in WebUntis: Profile › Mobile access › Show QR code.';

/** First screen for a device with no token: scan the WebUntis mobile-access QR code. */
export function ScanScreen() {
  const insets = useSafeAreaInsets();
  const { markSignedIn } = useSession();
  const [permission, requestPermission] = useCameraPermissions();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // expo-camera on web always says canAskAgain, so remember that a request we made came back denied.
  const [refused, setRefused] = useState(false);
  // A camera fires onBarcodeScanned many times a second; this guards the in-flight scan.
  const handling = useRef(false);

  async function onScanned(raw: string) {
    if (handling.current) return;
    handling.current = true;
    setBusy(true);

    const payload = parseQrPayload(raw);
    if (!payload) {
      setError("That QR code didn't work. It isn't a WebUntis mobile-access code.");
      setBusy(false);
      return;
    }
    try {
      await api.scanQr(payload);
      markSignedIn();
    } catch (e) {
      console.warn('QR scan failed', e);
      setError(
        e instanceof NetworkError
          ? "Couldn't reach the server. Check your connection and try again."
          : e instanceof ApiError && e.status === 400
            ? "That QR code didn't work. It may have expired. Generate a fresh one and try again."
            : 'Something went wrong linking your account. Try again.',
      );
      setBusy(false);
    }
  }

  async function allowCamera() {
    const result = await requestPermission();
    setRefused(!result.granted && result.status === 'denied');
  }

  function retry() {
    setError(null);
    handling.current = false;
  }

  if (!permission) return <View style={styles.screen} />;

  if (!permission.granted) {
    const blocked = !permission.canAskAgain || refused;
    return (
      <View style={[styles.screen, styles.centered, { paddingTop: insets.top }]}>
        <View style={styles.sheet}>
          <Text style={styles.title} accessibilityRole="header">
            Camera access needed
          </Text>
          <Text style={styles.body}>
            TimeForYou links your timetable by scanning a QR code, so it needs the camera.
            {blocked
              ? Platform.OS === 'web'
                ? ' The browser did not give access: it is blocked for this site or no camera was found. Allow the camera in the address bar or site settings, then try again.'
                : ' Camera access is blocked: turn it on for TimeForYou in your device settings.'
              : ''}
          </Text>
          {permission.canAskAgain ? (
            <Button label={refused ? 'Try again' : 'Allow camera'} onPress={allowCamera} />
          ) : Platform.OS !== 'web' ? (
            <Button label="Open settings" onPress={() => Linking.openSettings()} />
          ) : null}
        </View>
      </View>
    );
  }

  return (
    <View style={styles.screen}>
      <CameraView
        style={StyleSheet.absoluteFill}
        facing="back"
        barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
        onBarcodeScanned={error || busy ? undefined : ({ data }) => onScanned(data)}
      />
      <View style={[styles.overlay, { paddingTop: insets.top + Spacing.three }]} pointerEvents="none">
        <Text style={styles.heading} accessibilityRole="header">
          Scan QR code
        </Text>
        <View style={styles.frame} />
        <Text style={styles.prompt}>Point your camera at the WebUntis mobile-access QR code</Text>
      </View>

      <View style={[styles.bottom, { paddingBottom: insets.bottom + Spacing.three }]}>
        <View style={styles.sheet}>
          {error ? (
            <>
              <Text style={styles.title} accessibilityRole="alert">
                {error}
              </Text>
              <Text style={styles.body}>{FIND_CODE_HINT}</Text>
              <Button label="Try again" onPress={retry} />
            </>
          ) : busy ? (
            <View style={styles.row}>
              <ActivityIndicator color={Brand.navy} />
              <Text style={styles.body}>Linking your account…</Text>
            </View>
          ) : (
            <Text style={styles.body}>{FIND_CODE_HINT}</Text>
          )}
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#0B1220' },
  centered: { justifyContent: 'center', padding: Spacing.three },
  overlay: { position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, alignItems: 'center', gap: Spacing.four },
  heading: { fontFamily: Font.textBold, fontSize: 20, color: Brand.white },
  frame: {
    width: 260,
    height: 260,
    marginTop: Spacing.five,
    borderRadius: Radius.card,
    borderWidth: 3,
    borderColor: Brand.orange,
  },
  prompt: {
    fontFamily: Font.text,
    fontSize: 16,
    color: Brand.white,
    textAlign: 'center',
    paddingHorizontal: Spacing.five,
  },
  bottom: { position: 'absolute', left: 0, right: 0, bottom: 0, alignItems: 'center', padding: Spacing.three },
  sheet: {
    width: '100%',
    maxWidth: MaxContentWidth,
    gap: Spacing.three,
    padding: Spacing.four,
    borderRadius: Radius.card,
    backgroundColor: Brand.white,
  },
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  title: { fontFamily: Font.textBold, fontSize: 18, color: Brand.navy },
  body: { fontFamily: Font.text, fontSize: 14, color: '#0F172A' },
});
