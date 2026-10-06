import {
  HankenGrotesk_400Regular,
  HankenGrotesk_600SemiBold,
  HankenGrotesk_700Bold,
} from '@expo-google-fonts/hanken-grotesk';
import {
  PublicSans_500Medium,
  PublicSans_600SemiBold,
} from '@expo-google-fonts/public-sans';
import { useFonts } from 'expo-font';

import { FontFamily } from '@/shared/theme/theme';

/** Family names for each weight we load. Use these as `fontFamily`, never a bare `fontWeight`. */
export const Font = {
  text: FontFamily.text,
  textSemiBold: `${FontFamily.text}-SemiBold`,
  textBold: `${FontFamily.text}-Bold`,
  label: FontFamily.label,
  labelSemiBold: `${FontFamily.label}-SemiBold`,
} as const;

/** Loads Hanken Grotesk and Public Sans. Returns true once both are ready (or failed to load). */
export function useAppFonts(): boolean {
  const [loaded, error] = useFonts({
    [Font.text]: HankenGrotesk_400Regular,
    [Font.textSemiBold]: HankenGrotesk_600SemiBold,
    [Font.textBold]: HankenGrotesk_700Bold,
    [Font.label]: PublicSans_500Medium,
    [Font.labelSemiBold]: PublicSans_600SemiBold,
  });
  return loaded || error != null;
}
