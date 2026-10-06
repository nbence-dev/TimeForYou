import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Text, View } from 'react-native';
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

export function BottomNav({ active, onSelect, changesCount = 0 }: BottomNavProps) {
  const insets = useSafeAreaInsets();
  return (
    <View style={[styles.outer, { paddingBottom: Math.max(insets.bottom, Spacing.two) }]}>
      <View style={styles.pill}>
        <View style={styles.track}>
          {NAV_TABS.map((tab) => {
            const isActive = tab.key === active;
            const showBadge = tab.key === 'changes' && changesCount > 0;
            const fg = isActive ? Brand.white : Colors.light.textSecondary;
            return (
              <Pressable
                key={tab.key}
                accessibilityRole="tab"
                accessibilityLabel={tab.label}
                accessibilityState={{ selected: isActive }}
                onPress={() => onSelect(tab.key)}
                style={[styles.tab, isActive && styles.tabActive]}>
                <View>
                  <Ionicons name={tab.icon} size={22} color={fg} />
                  {showBadge ? (
                    <View style={styles.badge} accessibilityLabel={`${changesCount} unseen changes`}>
                      <Text style={styles.badgeText}>{changesCount > 9 ? '9+' : changesCount}</Text>
                    </View>
                  ) : null}
                </View>
                <Text style={[styles.label, { color: fg }]} numberOfLines={1}>
                  {tab.label}
                </Text>
              </Pressable>
            );
          })}
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  outer: { paddingHorizontal: Spacing.three, backgroundColor: 'transparent' },
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
    padding: Spacing.one,
  },
  tab: {
    flex: 1,
    minHeight: 56,
    alignItems: 'center',
    justifyContent: 'center',
    gap: Spacing.half,
    borderRadius: Radius.pill,
  },
  tabActive: { backgroundColor: Brand.navy },
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
