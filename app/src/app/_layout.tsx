import { Stack } from 'expo-router';

import { SessionProvider, useSession } from '@/shared/session/session';
import { useAppFonts } from '@/shared/theme/fonts';

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
  if (signedIn === null) return null;
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Protected guard={signedIn}>
        <Stack.Screen name="(tabs)" />
      </Stack.Protected>
      <Stack.Protected guard={!signedIn}>
        <Stack.Screen name="scan" />
      </Stack.Protected>
    </Stack>
  );
}
