import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Button } from '@/shared/components/button';
import { useSession } from '@/shared/session/session';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing } from '@/shared/theme/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

const STEPS: { title: string; detail: string; icon: IconName; filled?: boolean }[] = [
  { title: 'Tap the Share button in Safari', detail: 'Look at the bottom toolbar below', icon: 'share-outline' },
  { title: 'Choose Add to Home Screen', detail: 'Scroll down the share sheet menu', icon: 'add-outline' },
  {
    title: 'Open TimeForYou from Home',
    detail: 'Launch the app icon to turn on live alerts',
    icon: 'calendar-outline',
    filled: true,
  },
];

/**
 * Shown on iOS Safari right after a successful scan (#67). iOS only delivers Web Push to a site
 * launched from the Home Screen, so this walks the student through Add to Home Screen. A soft gate:
 * both buttons go on into the app, and skipping leaves the "notifications are off" banner instead.
 */
export function AddToHomeScreenScreen() {
  const insets = useSafeAreaInsets();
  const { closeInstallGuide } = useSession();

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.topBar}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Skip for now"
          hitSlop={8}
          onPress={closeInstallGuide}
          style={styles.close}>
          <Ionicons name="close" size={26} color={Brand.navy} />
        </Pressable>
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        <View style={styles.column}>
          <View style={styles.hero}>
            <View style={styles.heroIcon}>
              <Ionicons name="notifications-outline" size={40} color={Brand.navy} />
              <View style={styles.heroBadge}>
                <Ionicons name="add" size={16} color={Brand.white} />
              </View>
            </View>
            <Text style={styles.title} accessibilityRole="header">
              Turn on notifications
            </Text>
            <Text style={styles.subtitle}>
              On iPhone, alerts only work once TimeForYou is on your Home Screen.
            </Text>
          </View>

          {STEPS.map((step, i) => (
            <View key={step.title} style={styles.step}>
              <View style={styles.stepNumber}>
                <Text style={styles.stepNumberText}>{i + 1}</Text>
              </View>
              <View style={styles.stepText}>
                <Text style={styles.stepTitle}>{step.title}</Text>
                <Text style={styles.stepDetail}>{step.detail}</Text>
              </View>
              <View style={[styles.stepIcon, step.filled && styles.stepIconFilled]}>
                <Ionicons name={step.icon} size={22} color={step.filled ? Brand.white : Brand.navy} />
              </View>
            </View>
          ))}

          <View style={styles.toolbarCard} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
            <View style={styles.hint}>
              <Ionicons name="hand-left-outline" size={14} color={Brand.white} />
              <Text style={styles.hintText}>Tap here first</Text>
            </View>
            <View style={styles.toolbar}>
              <Ionicons name="chevron-back" size={24} color={Colors.light.textSecondary} />
              <Ionicons name="chevron-forward" size={24} color={Colors.light.backgroundSelected} />
              <View style={styles.shareRing}>
                <Ionicons name="share-outline" size={22} color={Brand.orange} />
              </View>
              <Ionicons name="book-outline" size={22} color={Colors.light.textSecondary} />
              <Ionicons name="copy-outline" size={22} color={Colors.light.textSecondary} />
            </View>
            <Text style={styles.toolbarCaption}>Safari&apos;s bottom toolbar</Text>
          </View>
        </View>
      </ScrollView>

      <View style={[styles.footer, { paddingBottom: insets.bottom + Spacing.three }]}>
        <View style={styles.column}>
          <Button label="I've added it" onPress={closeInstallGuide} />
          <Pressable accessibilityRole="button" onPress={closeInstallGuide} style={styles.skip}>
            <Text style={styles.skipText}>Skip for now</Text>
          </Pressable>
          <Text style={styles.footnote}>You can still use the app, but you won&apos;t get change alerts.</Text>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  topBar: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    paddingHorizontal: Spacing.three,
    paddingVertical: Spacing.two,
    borderBottomWidth: 1,
    borderBottomColor: Brand.border,
  },
  close: { padding: Spacing.one },
  content: { padding: Spacing.three, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  hero: { alignItems: 'center', gap: Spacing.two, paddingVertical: Spacing.three },
  heroIcon: {
    width: 88,
    height: 88,
    borderRadius: Radius.card,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.backgroundElement,
    borderWidth: 1,
    borderColor: Brand.border,
  },
  heroBadge: {
    position: 'absolute',
    top: -8,
    right: -8,
    width: 28,
    height: 28,
    borderRadius: Radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Brand.orange,
  },
  title: { fontFamily: Font.textBold, fontSize: 26, color: Brand.navy, textAlign: 'center' },
  subtitle: { fontFamily: Font.text, fontSize: 16, lineHeight: 24, color: Colors.light.textSecondary, textAlign: 'center' },
  step: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  stepNumber: {
    width: 36,
    height: 36,
    borderRadius: Radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.backgroundElement,
  },
  stepNumberText: { fontFamily: Font.labelSemiBold, fontSize: 15, color: Brand.navy },
  stepText: { flex: 1, gap: Spacing.half },
  stepTitle: { fontFamily: Font.textBold, fontSize: 16, color: Colors.light.text },
  stepDetail: { fontFamily: Font.text, fontSize: 14, color: Colors.light.textSecondary },
  stepIcon: {
    width: 44,
    height: 44,
    borderRadius: Radius.base,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: Brand.borderStrong,
    backgroundColor: Colors.light.backgroundElement,
  },
  stepIconFilled: { backgroundColor: Brand.navy, borderColor: Brand.navy },
  toolbarCard: {
    alignItems: 'center',
    gap: Spacing.two,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Colors.light.backgroundElement,
  },
  hint: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.one,
    paddingHorizontal: Spacing.three,
    paddingVertical: Spacing.one,
    borderRadius: Radius.pill,
    backgroundColor: Brand.orange,
  },
  hintText: { fontFamily: Font.labelSemiBold, fontSize: 13, color: Brand.white },
  toolbar: {
    width: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-around',
    paddingVertical: Spacing.three,
    borderRadius: Radius.base,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  shareRing: {
    width: 44,
    height: 44,
    borderRadius: Radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 2,
    borderColor: Brand.orange,
  },
  toolbarCaption: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  footer: {
    alignItems: 'center',
    paddingHorizontal: Spacing.three,
    paddingTop: Spacing.three,
    borderTopWidth: 1,
    borderTopColor: Brand.border,
    backgroundColor: Colors.light.background,
  },
  skip: { alignSelf: 'center', paddingVertical: Spacing.two },
  skipText: { fontFamily: Font.textSemiBold, fontSize: 16, color: Brand.navy },
  footnote: { fontFamily: Font.text, fontSize: 13, color: Colors.light.textSecondary, textAlign: 'center' },
});