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

/** A device with a token skips onboarding; one without (or after a 401) lands on the scan screen. */
function Routes() {
  const { signedIn } = useSession();
  const ready = signedIn !== null;
  return (
    <View style={{ flex: 1 }}>
      {ready && (
        <Stack screenOptions={{ headerShown: false }}>
          <Stack.Protected guard={signedIn}>
            <Stack.Screen name="(tabs)" />
            <Stack.Screen name="lesson/[id]" options={{ presentation: 'modal' }} />
            <Stack.Screen name="note/[lessonId]" options={{ presentation: 'modal' }} />
          </Stack.Protected>
          <Stack.Protected guard={!signedIn}>
            <Stack.Screen name="scan" />
          </Stack.Protected>
        </Stack>
      )}
      <BrandSplash ready={ready} />
    </View>
  );
}
