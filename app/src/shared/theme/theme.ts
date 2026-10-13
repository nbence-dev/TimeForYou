/**
 * Below are the colors that are used in the app. The colors are defined in the light and dark mode.
 * There are many other ways to style your app. For example, [Nativewind](https://www.nativewind.dev/), [Tamagui](https://tamagui.dev/), [unistyles](https://reactnativeunistyles.vercel.app), etc.
 */

import '@/shared/theme/global.css';

import { Platform } from 'react-native';

export const Colors = {
  light: {
    text: '#0F172A',
    background: '#F8FAFC',
    backgroundElement: '#F1F5F9',
    backgroundSelected: '#E2E8F0',
    textSecondary: '#64748B',
  },
  dark: {
    text: '#ffffff',
    background: '#000000',
    backgroundElement: '#212225',
    backgroundSelected: '#2E3135',
    textSecondary: '#B0B4BA',
  },
} as const;

/**
 * TimeForYou brand and status colours. Single source of truth: see docs/design/README.md.
 * Never hard-code a hex value in a feature; import it from here.
 */
export const Brand = {
  /** App bars, primary buttons, active navigation, headings. */
  navy: '#15315D',
  /** Accent only: hero action, active day, current-time line, unseen dots, focus rings. */
  orange: '#CC5A01',
  white: '#FFFFFF',
  border: '#E2E8F0',
  borderStrong: '#CBD5E1',
  /** Backdrop for the bottom-nav shadow. */
  shadow: '#0F172A',
  /** Behind the camera view on the scan screen. */
  scanBackdrop: '#0B1220',
} as const;

/** Lesson status colours. `strip` is the 4px card edge; `bg`/`text`/`border` style the chip. */
export const Status = {
  onTime: { strip: '#059669', bg: '#ECFDF5', text: '#065F46', border: '#A7F3D0' },
  changed: { strip: '#A16207', bg: '#FEF9C3', text: '#854D0E', border: '#FDE68A' },
  cancelled: {
    strip: '#DC2626',
    bg: '#FEE2E2',
    text: '#991B1B',
    border: '#FECACA',
    /** Whole-card background for a cancelled lesson. */
    surface: '#FEF2F2',
  },
  live: { strip: '#CC5A01', bg: '#ECFDF5', text: '#065F46', border: '#A7F3D0' },
} as const;

/**
 * A lesson card's visual state. Not the API's lesson status (`scheduled | cancelled | irregular`,
 * see `@/shared/types/lesson`): use `toCardStatus` to get from one to the other.
 */
export type CardStatus = keyof typeof Status;

export const Radius = {
  base: 8,
  /** Banners and notices. */
  banner: 12,
  card: 16,
  pill: 9999,
} as const;

/** Title sizes: one per level, so the same kind of title is the same size on every screen. */
export const TitleSize = {
  /** The title of a screen: in `ScreenHeader`, on every screen. */
  screen: 24,
  /** A heading inside a screen: the day or week being shown, an empty-state title. */
  section: 20,
  /** A card's heading. */
  card: 18,
} as const;

/** Font family names. The font files are loaded by the shared UI components ticket (#81). */
export const FontFamily = {
  text: 'HankenGrotesk',
  label: 'PublicSans',
} as const;

export type ThemeColor = keyof typeof Colors.light & keyof typeof Colors.dark;

export const Fonts = Platform.select({
  ios: {
    /** iOS `UIFontDescriptorSystemDesignDefault` */
    sans: 'system-ui',
    /** iOS `UIFontDescriptorSystemDesignSerif` */
    serif: 'ui-serif',
    /** iOS `UIFontDescriptorSystemDesignRounded` */
    rounded: 'ui-rounded',
    /** iOS `UIFontDescriptorSystemDesignMonospaced` */
    mono: 'ui-monospace',
  },
  default: {
    sans: 'normal',
    serif: 'serif',
    rounded: 'normal',
    mono: 'monospace',
  },
  web: {
    sans: 'var(--font-display)',
    serif: 'var(--font-serif)',
    rounded: 'var(--font-rounded)',
    mono: 'var(--font-mono)',
  },
});

export const Spacing = {
  half: 2,
  one: 4,
  two: 8,
  three: 16,
  four: 24,
  five: 32,
  six: 64,
} as const;

export const BottomTabInset = Platform.select({ ios: 50, android: 80 }) ?? 0;
export const MaxContentWidth = 800;
