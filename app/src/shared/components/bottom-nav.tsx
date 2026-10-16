import { Ionicons } from '@expo/vector-icons';
import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import Animated, {
  interpolateColor,
  useAnimatedStyle,
  useDerivedValue,
  useSharedValue,
  withSpring,
  type SharedValue,
} from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Brand, Colors, Radius, Spacing } from '@/shared/theme/theme';
import { Font } from '@/shared/theme/fonts';

export type NavTabKey = 'today' | 'timetable' | 'changes' | 'settings';

type IconName = React.ComponentProps<typeof Ionicons>['name'];

export const NAV_TABS: readonly { key: NavTabKey; label: string; icon: IconName }[] = [
  { key: 'today', label: 'Today', icon: 'calendar-outline' },
  { key: 'timetable', label: 'Timetable', icon: 'grid-outline' },
  { key: 'changes', label: 'Changes', icon: 'sync-outline' },
  { key: 'settings', label: 'Settings', icon: 'settings-outline' },
];

export type BottomNavProps = {
  active: NavTabKey;
  onSelect: (key: NavTabKey) => void;
  /** Unseen changes. The orange badge on Changes shows when this is above zero. */
  changesCount?: number;
};

const TRACK_PADDING = Spacing.one;

/** The navy pill glides to the chosen tab; icon and label fade colour with it, so nothing snaps. */
function NavTab({
  tab,
  index,
  position,
  onPress,
  isActive,
  badge,
}: {
  tab: (typeof NAV_TABS)[number];
  index: number;
  position: SharedValue<number>;
  onPress: () => void;
  isActive: boolean;
  badge: number;
}) {
  // 1 when the pill is over this tab, 0 when it is a tab or more away.
  const closeness = useDerivedValue(() => Math.max(0, 1 - Math.abs(position.value - index)));
  const labelStyle = useAnimatedStyle(() => ({
    color: interpolateColor(closeness.value, [0, 1], [Colors.light.textSecondary, Brand.white]),
  }));
  const whiteIcon = useAnimatedStyle(() => ({ opacity: closeness.value }));
  const greyIcon = useAnimatedStyle(() => ({ opacity: 1 - closeness.value }));

  return (
    <Pressable
      accessibilityRole="tab"
      accessibilityLabel={tab.label}
      accessibilityState={{ selected: isActive }}
      onPress={onPress}
      style={styles.tab}>
      <View style={styles.iconBox}>
        <Animated.View style={greyIcon}>
          <Ionicons name={tab.icon} size={22} color={Colors.light.textSecondary} />
        </Animated.View>
        <Animated.View style={[StyleSheet.absoluteFill, whiteIcon]}>
          <Ionicons name={tab.icon} size={22} color={Brand.white} />
        </Animated.View>
        {badge > 0 ? (
          <View style={styles.badge} accessibilityLabel={`${badge} unseen changes`}>
            <Text style={styles.badgeText}>{badge > 9 ? '9+' : badge}</Text>
          </View>
        ) : null}
      </View>
      <Animated.Text style={[styles.label, labelStyle]} numberOfLines={1}>
        {tab.label}
      </Animated.Text>
    </Pressable>
  );
}

export function BottomNav({ active, onSelect, changesCount = 0 }: BottomNavProps) {
  const insets = useSafeAreaInsets();
  const activeIndex = Math.max(0, NAV_TABS.findIndex((t) => t.key === active));
  const position = useSharedValue(activeIndex);
  const [trackWidth, setTrackWidth] = useState(0);

  useEffect(() => {
    position.value = withSpring(activeIndex, { damping: 20, stiffness: 220, mass: 0.8 });
  }, [activeIndex, position]);

  const tabWidth = (trackWidth - TRACK_PADDING * 2) / NAV_TABS.length;
  const pillStyle = useAnimatedStyle(() => ({ transform: [{ translateX: position.value * tabWidth }] }));
  return (
    <View style={[styles.outer, { paddingBottom: Math.max(insets.bottom, Spacing.two) }]}>
      <View style={styles.pill}>
        <View style={styles.track} onLayout={(e) => setTrackWidth(e.nativeEvent.layout.width)}>
          {trackWidth > 0 ? <Animated.View style={[styles.indicator, { width: tabWidth }, pillStyle]} /> : null}
          {NAV_TABS.map((tab, i) => (
            <NavTab
              key={tab.key}
              tab={tab}
              index={i}
              position={position}
              isActive={tab.key === active}
              badge={tab.key === 'changes' ? changesCount : 0}
              onPress={() => onSelect(tab.key)}
            />
          ))}
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  // The page colour, not transparent: behind a transparent bar the navigation library paints its own
  // default grey (#F2F2F2), which showed as a darker band under the nav.
  outer: { paddingHorizontal: Spacing.three, backgroundColor: Colors.light.background },
  pill: {
    backgroundColor: Brand.white,
    borderRadius: Radius.pill,
    padding: Spacing.one,
    borderWidth: 1,
    borderColor: Brand.border,
    shadowColor: Brand.shadow,
    shadowOpacity: 0.12,
    shadowRadius: 12,
    shadowOffset: { width: 0, height: 4 },
    elevation: 8,
  },
  track: {
    flexDirection: 'row',
    backgroundColor: Colors.light.backgroundElement,
    borderRadius: Radius.pill,
    padding: TRACK_PADDING,
  },
  indicator: {
    position: 'absolute',
    top: TRACK_PADDING,
    bottom: TRACK_PADDING,
    left: TRACK_PADDING,
    borderRadius: Radius.pill,
    backgroundColor: Brand.navy,
  },
  iconBox: { width: 22, height: 22 },
  tab: {
    flex: 1,
    minHeight: 56,
    alignItems: 'center',
    justifyContent: 'center',
    gap: Spacing.half,
    borderRadius: Radius.pill,
  },
  label: { fontFamily: Font.labelSemiBold, fontSize: 11 },
  badge: {
    position: 'absolute',
    top: -6,
    right: -10,
    minWidth: 16,
    height: 16,
    paddingHorizontal: Spacing.one,
    borderRadius: Radius.pill,
    backgroundColor: Brand.orange,
    alignItems: 'center',
    justifyContent: 'center',
  },
  badgeText: { fontFamily: Font.labelSemiBold, fontSize: 10, color: Brand.white },
});
