import { Ionicons } from '@expo/vector-icons';
import type { ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, Radius, Spacing, TitleSize } from '@/shared/theme/theme';

/**
 * The top of every screen: the same inset, the same title size, the same back and close controls.
 * Pushed screens (Help, Calendar) pass `onBack`, which puts the back control on the left. Modal-style
 * screens (Lesson, Note, Add to Home Screen) pass `onClose`, which puts the close control on the right.
 * Put it inside the screen's column: it adds the safe-area inset to the top itself.
 */
export function ScreenHeader({
  title,
  onBack,
  onClose,
  closeLabel = 'Close',
  right,
  above,
  below,
}: {
  title: string;
  onBack?: () => void;
  onClose?: () => void;
  closeLabel?: string;
  /** A small action or status on the right, before the close control. */
  right?: ReactNode;
  /** Shown over the title, such as a status chip. */
  above?: ReactNode;
  /** Shown under the title, such as the lesson's time and room. */
  below?: ReactNode;
}) {
  const insets = useSafeAreaInsets();
  const tall = !!(above || below);

  return (
    <View style={[styles.row, tall && styles.rowTall, { paddingTop: insets.top + Spacing.three }]}>
      {onBack ? <HeaderButton icon="arrow-back" label="Back" onPress={onBack} /> : null}
      <View style={styles.titleBlock}>
        {above}
        <Text style={styles.title} accessibilityRole="header">
          {title}
        </Text>
        {below}
      </View>
      {right}
      {onClose ? <HeaderButton icon="close" label={closeLabel} onPress={onClose} /> : null}
    </View>
  );
}

function HeaderButton({
  icon,
  label,
  onPress,
}: {
  icon: 'arrow-back' | 'close';
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} hitSlop={8} onPress={onPress} style={styles.button}>
      <Ionicons name={icon} size={22} color={Brand.navy} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  rowTall: { alignItems: 'flex-start' },
  titleBlock: { flex: 1, gap: Spacing.two, alignItems: 'flex-start' },
  title: { fontFamily: Font.textBold, fontSize: TitleSize.screen, color: Brand.navy },
  button: {
    width: 44,
    height: 44,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
