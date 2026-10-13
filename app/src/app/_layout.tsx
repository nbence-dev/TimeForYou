import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { View } from 'react-native';

import { BrandSplash } from '@/shared/components/brand-splash';
import { SessionProvider, useSession } from '@/shared/session/session';
import { useAppFonts } from '@/shared/theme/fonts';

// Keep the native splash up until BrandSplash has painted; it releases it itself.
SplashScreen.preventAutoHideAsync().catch(() => {});

export default function RootLayout() {
  const fontsReady = useAppFonts();
  if (!fontsReady) return null;
  return (
    <SessionProvider>
      <Routes />
    </SessionProvider>
  );
}

/**
 * A device with a token skips onboarding; one without (or after a 401) lands on the scan screen.
 * On iPhone Safari outside the Home Screen, a device with no token sees the install guide before the
 * scan (so the scan happens inside the installed app), and the banner can reopen it later (#67).
 */
function Routes() {
  const { signedIn, showingInstallGuide } = useSession();
  const ready = signedIn !== null;
  return (
    <View style={{ flex: 1 }}>
      {ready && (
        <Stack screenOptions={{ headerShown: false }}>
          <Stack.Protected guard={signedIn === true && !showingInstallGuide}>
            <Stack.Screen name="(tabs)" />
            <Stack.Screen name="calendar" />
            <Stack.Screen name="help" />
            <Stack.Screen name="devices" />
            <Stack.Screen name="lesson/[id]" options={{ presentation: 'modal' }} />
            <Stack.Screen name="note/[lessonId]" options={{ presentation: 'modal' }} />
          </Stack.Protected>
          <Stack.Protected guard={showingInstallGuide}>
            <Stack.Screen name="add-to-home-screen" />
          </Stack.Protected>
          <Stack.Protected guard={!signedIn && !showingInstallGuide}>
            <Stack.Screen name="scan" />
          </Stack.Protected>
        </Stack>
      )}
      <BrandSplash ready={ready} />
    </View>
  );
}