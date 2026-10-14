import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet } from 'react-native';

import { Brand, Radius } from '@/shared/theme/theme';

/** The previous/next arrow for stepping through days and weeks. One look everywhere. */
export function NavArrow({
  direction,
  label,
  disabled = false,
  onPress,
}: {
  direction: 'back' | 'forward';
  /** What it does, for screen readers: "Previous day", "Next week". */
  label: string;
  disabled?: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      hitSlop={8}
      style={[styles.arrow, disabled && styles.disabled]}>
      <Ionicons name={direction === 'back' ? 'chevron-back' : 'chevron-forward'} size={22} color={Brand.navy} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  arrow: {
    width: 40,
    height: 40,
    borderRadius: Radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Brand.white,
    borderWidth: 1,
    borderColor: Brand.borderStrong,
  },
  disabled: { opacity: 0.35 },
});
