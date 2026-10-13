import { StyleSheet, View, type ViewProps } from 'react-native';

import { Brand, Radius, Spacing, Status } from '@/shared/theme/theme';

/** The white bordered surface cards share: 16px radius, 1px border, 16px padding. */
export function Card({
  tone = 'default',
  style,
  ...rest
}: ViewProps & {
  /** `danger` is the red surface for a destructive confirmation. */
  tone?: 'default' | 'danger';
}) {
  return <View style={[styles.card, tone === 'danger' && styles.danger, style]} {...rest} />;
}

const styles = StyleSheet.create({
  card: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  danger: { borderColor: Status.cancelled.border, backgroundColor: Status.cancelled.surface },
});
