import { Pressable, StyleSheet, Text, type PressableProps } from 'react-native';

import { Brand, Colors, Radius, Spacing, Status } from '@/shared/theme/theme';
import { Font } from '@/shared/theme/fonts';

export type ButtonVariant = 'primary' | 'accent' | 'secondary' | 'destructive';

export type ButtonProps = Omit<PressableProps, 'children'> & {
  label: string;
  variant?: ButtonVariant;
};

const VARIANTS: Record<ButtonVariant, { bg: string; fg: string; border: string }> = {
  primary: { bg: Brand.navy, fg: Brand.white, border: Brand.navy },
  accent: { bg: Brand.orange, fg: Brand.white, border: Brand.orange },
  secondary: { bg: Brand.white, fg: Brand.navy, border: Brand.borderStrong },
  destructive: { bg: Brand.white, fg: Status.cancelled.strip, border: Status.cancelled.strip },
};

export function Button({ label, variant = 'primary', disabled, style, ...rest }: ButtonProps) {
  const v = VARIANTS[variant];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: !!disabled }}
      disabled={disabled}
      style={(state) => [
        styles.base,
        { backgroundColor: v.bg, borderColor: v.border },
        state.pressed && styles.pressed,
        disabled && styles.disabled,
        typeof style === 'function' ? style(state) : style,
      ]}
      {...rest}>
      <Text style={[styles.label, { color: v.fg }]}>{label}</Text>
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
  label: { fontFamily: Font.textSemiBold, fontSize: 16 },
  pressed: { opacity: 0.85 },
  disabled: { backgroundColor: Colors.light.backgroundSelected, opacity: 0.6 },
});
