import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps, ReactNode } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, Radius, Spacing, TitleSize } from '@/shared/theme/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

/**
 * Nothing to show yet, or nothing at all: loading, empty and offline look the same on every screen.
 * An icon (or a spinner while `loading`) in a circle, a title, optional text, then any actions.
 */
export function EmptyState({
  icon,
  loading = false,
  title,
  text,
  children,
}: {
  icon?: IconName;
  loading?: boolean;
  title: string;
  text?: string;
  children?: ReactNode;
}) {
  return (
    <View style={styles.state} accessibilityLiveRegion="polite">
      {loading || icon ? (
        <View style={styles.circle}>
          {loading ? (
            <ActivityIndicator size="large" color={Brand.navy} />
          ) : icon ? (
            <Ionicons name={icon} size={36} color={Brand.navy} />
          ) : null}
        </View>
      ) : null}
      <Text style={styles.title}>{title}</Text>
      {text ? <Text style={styles.text}>{text}</Text> : null}
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  state: { alignItems: 'center', gap: Spacing.three, paddingVertical: Spacing.five, paddingHorizontal: Spacing.three },
  circle: {
    width: 96,
    height: 96,
    borderRadius: Radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.backgroundElement,
  },
  title: { fontFamily: Font.textBold, fontSize: TitleSize.section, color: Brand.navy, textAlign: 'center' },
  text: {
    fontFamily: Font.text,
    fontSize: 15,
    lineHeight: 22,
    color: Colors.light.textSecondary,
    textAlign: 'center',
  },
});
