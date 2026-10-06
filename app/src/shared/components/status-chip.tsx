import { StyleSheet, Text, View } from 'react-native';

import { Colors, Radius, Spacing, Status } from '@/shared/theme/theme';
import { Font } from '@/shared/theme/fonts';

export type StatusChipKind = 'cancelled' | 'roomChanged' | 'timeChanged' | 'live' | 'offline';

const CHIPS: Record<StatusChipKind, { label: string; bg: string; fg: string; border: string }> = {
  cancelled: { label: 'Cancelled', bg: Status.cancelled.bg, fg: Status.cancelled.text, border: Status.cancelled.border },
  roomChanged: { label: 'Room changed', bg: Status.changed.bg, fg: Status.changed.text, border: Status.changed.border },
  timeChanged: { label: 'Time changed', bg: Status.changed.bg, fg: Status.changed.text, border: Status.changed.border },
  live: { label: 'Live now', bg: Status.live.bg, fg: Status.live.text, border: Status.live.border },
  offline: {
    label: 'Offline',
    bg: Colors.light.backgroundElement,
    fg: Colors.light.textSecondary,
    border: Colors.light.backgroundSelected,
  },
};

export function StatusChip({ kind }: { kind: StatusChipKind }) {
  const chip = CHIPS[kind];
  return (
    <View
      accessibilityLabel={chip.label}
      style={[styles.chip, { backgroundColor: chip.bg, borderColor: chip.border }]}>
      <Text style={[styles.label, { color: chip.fg }]}>{chip.label.toUpperCase()}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  chip: {
    height: 24,
    paddingHorizontal: Spacing.two,
    borderRadius: Radius.pill,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    alignSelf: 'flex-start',
  },
  label: {
    fontFamily: Font.labelSemiBold,
    fontSize: 11,
    letterSpacing: 0.5,
    fontVariant: ['tabular-nums'],
  },
});
