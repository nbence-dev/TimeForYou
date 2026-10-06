import { Stack } from 'expo-router';

import { useAppFonts } from '@/shared/theme/fonts';

export default function RootLayout() {
  const fontsReady = useAppFonts();
  if (!fontsReady) return null;
  return <Stack screenOptions={{ headerShown: false }} />;
}
