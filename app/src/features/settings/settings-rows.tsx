import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps, ReactNode } from 'react';
import { Pressable, StyleSheet, Switch, Text, View } from 'react-native';

import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, Radius, Spacing } from '@/shared/theme/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

export function Section({ label, children }: { label: string; children: ReactNode }) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionLabel}>{label.toUpperCase()}</Text>
      <View style={styles.card}>{children}</View>
    </View>
  );
}

export function Row({
  icon,
  title,
  subtitle,
  value,
  soon,
  onPress,
  disabled,
  arrow,
}: {
  icon: IconName;
  title: string;
  subtitle?: string;
  value?: string;
  /** Not built yet: dimmed, tagged "Soon", and does nothing. */
  soon?: boolean;
  onPress?: () => void;
  disabled?: boolean;
  /** A chevron: this row opens another screen. */
  arrow?: boolean;
}) {
  const inert = soon || disabled;
  return (
    <Pressable
      accessibilityRole={onPress && !soon ? 'button' : undefined}
      accessibilityState={{ disabled: !!inert }}
      accessibilityLabel={soon ? `${title}, coming soon` : undefined}
      disabled={inert || !onPress}
      onPress={onPress}
      style={[styles.row, soon && styles.rowSoon, disabled && !soon && styles.rowBusy]}>
      <View style={styles.rowIcon}>
        <Ionicons name={icon} size={20} color={Brand.navy} />
      </View>
      <View style={styles.rowText}>
        <Text style={styles.rowTitle}>{title}</Text>
        {subtitle ? <Text style={styles.rowSubtitle}>{subtitle}</Text> : null}
      </View>
      {soon ? (
        <SoonTag />
      ) : value ? (
        <Text style={styles.rowValue}>{value}</Text>
      ) : arrow ? (
        <Ionicons name="chevron-forward" size={18} color={Colors.light.textSecondary} />
      ) : null}
    </Pressable>
  );
}

/**
 * A row with an on/off switch. `value` is null while it is still loading: the switch shows off and
 * can't be used until the real value arrives.
 */
export function ToggleRow({
  icon,
  title,
  subtitle,
  value,
  onChange,
  disabled,
}: {
  icon: IconName;
  title: string;
  subtitle?: string;
  value: boolean | null;
  onChange: (next: boolean) => void;
  disabled?: boolean;
}) {
  const inert = disabled || value === null;
  return (
    <View style={[styles.row, inert && styles.rowBusy]}>
      <View style={styles.rowIcon}>
        <Ionicons name={icon} size={20} color={Brand.navy} />
      </View>
      <View style={styles.rowText}>
        <Text style={styles.rowTitle}>{title}</Text>
        {subtitle ? <Text style={styles.rowSubtitle}>{subtitle}</Text> : null}
      </View>
      <Switch
        accessibilityLabel={title}
        value={value ?? false}
        onValueChange={onChange}
        disabled={inert}
        trackColor={{ false: Brand.borderStrong, true: Brand.navy }}
        thumbColor={value ? Brand.orange : Brand.white}
        // react-native-web ignores thumbColor for the "on" state and falls back to teal.
        // Not in the native SwitchProps typings, so it goes in via a spread.
        {...{ activeThumbColor: Brand.orange }}
      />
    </View>
  );
}

/** The small "Soon" tag on a row whose feature is not built yet. */
export function SoonTag() {
  return (
    <View style={styles.soon}>
      <Text style={styles.soonText}>Soon</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  section: { gap: Spacing.two },
  sectionLabel: { fontFamily: Font.labelSemiBold, fontSize: 12, letterSpacing: 1, color: Colors.light.textSecondary },
  card: {
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
    overflow: 'hidden',
  },

  row: {
    minHeight: 60,
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.three,
    padding: Spacing.three,
  },
  rowSoon: { opacity: 0.55 },
  rowBusy: { opacity: 0.6 },
  rowIcon: {
    width: 36,
    height: 36,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
    alignItems: 'center',
    justifyContent: 'center',
  },
  rowText: { flex: 1, gap: Spacing.half },
  rowTitle: { fontFamily: Font.textSemiBold, fontSize: 16, color: Brand.navy },
  rowSubtitle: { fontFamily: Font.text, fontSize: 13, color: Colors.light.textSecondary },
  rowValue: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  soon: {
    paddingHorizontal: Spacing.two,
    paddingVertical: Spacing.half,
    borderRadius: Radius.pill,
    borderWidth: 1,
    borderColor: Brand.borderStrong,
  },
  soonText: { fontFamily: Font.labelSemiBold, fontSize: 11, color: Colors.light.textSecondary },
});
