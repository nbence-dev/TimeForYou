import { ActivityIndicator, Pressable, StyleSheet, Text, View, type PressableProps } from 'react-native';

import { Brand, Colors, Radius, Spacing, Status } from '@/shared/theme/theme';
import { Font } from '@/shared/theme/fonts';

export type ButtonVariant = 'primary' | 'accent' | 'secondary' | 'destructive';

export type ButtonProps = Omit<PressableProps, 'children'> & {
  label: string;
  variant?: ButtonVariant;
  /** A request is in flight: shows a spinner and blocks taps, so a double tap cannot send twice. */
  loading?: boolean;
};

const VARIANTS: Record<ButtonVariant, { bg: string; fg: string; border: string }> = {
  primary: { bg: Brand.navy, fg: Brand.white, border: Brand.navy },
  accent: { bg: Brand.orange, fg: Brand.white, border: Brand.orange },
  secondary: { bg: Brand.white, fg: Brand.navy, border: Brand.borderStrong },
  destructive: { bg: Brand.white, fg: Status.cancelled.strip, border: Status.cancelled.strip },
};

export function Button({ label, variant = 'primary', disabled, loading = false, style, ...rest }: ButtonProps) {
  const v = VARIANTS[variant];
  const blocked = disabled || loading;
  return (
    <Pressable
      // First, so what the Button computes below (role, label, disabled and busy state, style) always
      // wins: a caller's extra props cannot undo the loading guard or its accessibility state.
      {...rest}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: !!blocked, busy: loading }}
      aria-busy={loading}
      disabled={blocked}
      style={(state) => [
        styles.base,
        { backgroundColor: v.bg, borderColor: v.border },
        state.pressed && styles.pressed,
        // A loading button keeps its colours (it is working, not unavailable).
        disabled && !loading && styles.disabled,
        typeof style === 'function' ? style(state) : style,
      ]}>
      <View style={styles.content}>
        {loading ? <ActivityIndicator size="small" color={v.fg} /> : null}
        <Text style={[styles.label, { color: v.fg }]}>{label}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    minHeight: 48,
    paddingHorizontal: Spacing.four,
    borderRadius: Radius.base,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  content: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: Spacing.two },
  label: { fontFamily: Font.textSemiBold, fontSize: 16 },
  pressed: { opacity: 0.85 },
  disabled: { backgroundColor: Colors.light.backgroundSelected, opacity: 0.6 },
});
