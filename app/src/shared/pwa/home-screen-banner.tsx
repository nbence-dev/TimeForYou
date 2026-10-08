import { Ionicons } from '@expo/vector-icons';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { needsHomeScreenInstall } from '@/shared/pwa/install';
import { useSession } from '@/shared/session/session';
import { Font } from '@/shared/theme/fonts';
import { Radius, Spacing, Status } from '@/shared/theme/theme';

// Dismissing hides the banner until the tab is closed (#67). sessionStorage can throw in private
// browsing or with site data blocked, so both helpers fail quietly.
const DISMISSED_KEY = 'timeforyou.homeScreenBannerDismissed';

function wasDismissed(): boolean {
  try {
    return window.sessionStorage.getItem(DISMISSED_KEY) === '1';
  } catch {
    return false;
  }
}

function rememberDismissed(): void {
  try {
    window.sessionStorage.setItem(DISMISSED_KEY, '1');
  } catch {
    // Storage unavailable: the banner still hides until the next reload.
  }
}

/**
 * "Notifications are off" strip for iPhone Safari outside the Home Screen (#67).
 * Renders nothing anywhere else. Only mounted once the session has loaded (after the browser
 * has started), so reading `window` in the initial state is safe for the static export.
 */
export function HomeScreenBanner() {
  const { openInstallGuide } = useSession();
  const [visible, setVisible] = useState(() => needsHomeScreenInstall() && !wasDismissed());

  if (!visible) return null;

  const dismiss = () => {
    rememberDismissed();
    setVisible(false);
  };

  return (
    <View style={styles.banner} accessibilityRole="alert">
      <Ionicons name="notifications-off-outline" size={20} color={Status.cancelled.text} />
      <View style={styles.body}>
        <Text style={styles.message}>
          Notifications are off. Add TimeForYou to your Home Screen to get live room change alerts.
        </Text>
        <Pressable onPress={openInstallGuide} accessibilityRole="link" hitSlop={8}>
          <Text style={styles.link}>Fix Settings →</Text>
        </Pressable>
      </View>
      <Pressable
        onPress={dismiss}
        accessibilityRole="button"
        accessibilityLabel="Dismiss"
        hitSlop={8}>
        <Ionicons name="close" size={20} color={Status.cancelled.text} />
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: Spacing.two,
    padding: Spacing.three,
    borderRadius: Radius.base,
    borderWidth: 1,
    backgroundColor: Status.cancelled.surface,
    borderColor: Status.cancelled.border,
  },
  body: { flex: 1, gap: Spacing.one },
  message: { fontFamily: Font.text, fontSize: 14, color: Status.cancelled.text },
  link: { fontFamily: Font.textSemiBold, fontSize: 14, color: Status.cancelled.text },
});