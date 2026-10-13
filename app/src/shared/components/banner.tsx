import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps, ReactNode } from 'react';
import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Brand, Radius, Spacing, Status } from '@/shared/theme/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

/**
 * A notice above the content: 12px radius, an optional icon, and an optional dismiss control in the
 * same corner every time. `info` is the white card, `alert` is the red one for something that needs
 * attention. Children are the 14px text, then any button.
 */
export function Banner({
  tone = 'info',
  icon,
  onDismiss,
  style,
  children,
}: {
  tone?: 'info' | 'alert';
  icon?: IconName;
  onDismiss?: () => void;
  style?: StyleProp<ViewStyle>;
  children: ReactNode;
}) {
  const alert = tone === 'alert';
  const iconColor = alert ? Status.cancelled.text : Brand.navy;

  return (
    <View
      accessibilityRole="alert"
      style={[styles.banner, alert ? styles.alertTone : styles.infoTone, style]}>
      {icon ? <Ionicons name={icon} size={20} color={iconColor} /> : null}
      <View style={styles.body}>{children}</View>
      {onDismiss ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Dismiss" hitSlop={8} onPress={onDismiss}>
          <Ionicons name="close" size={20} color={iconColor} />
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: Spacing.two,
    padding: Spacing.three,
    borderRadius: Radius.banner,
    borderWidth: 1,
  },
  infoTone: { backgroundColor: Brand.white, borderColor: Brand.border },
  alertTone: { backgroundColor: Status.cancelled.surface, borderColor: Status.cancelled.border },
  body: { flex: 1, gap: Spacing.two },
});
