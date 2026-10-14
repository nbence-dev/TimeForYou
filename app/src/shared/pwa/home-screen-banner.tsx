import { useState } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';

import { Banner } from '@/shared/components/banner';
import { usePushState } from '@/shared/push/push-state';
import { needsHomeScreenInstall } from '@/shared/pwa/install';
import { useSession } from '@/shared/session/session';
import { Font } from '@/shared/theme/fonts';
import { Status } from '@/shared/theme/theme';

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
  const push = usePushState();

  // Notifications are on after all (the student allowed them from the prompt strip), so the
  // "Notifications are off" warning would be wrong.
  if (!visible || push === 'registered') return null;

  const dismiss = () => {
    rememberDismissed();
    setVisible(false);
  };

  return (
    <Banner tone="alert" icon="notifications-off-outline" onDismiss={dismiss}>
      <Text style={styles.message}>
        Notifications are off. Add TimeForYou to your Home Screen to get live room change alerts.
      </Text>
      <Pressable onPress={openInstallGuide} accessibilityRole="link" hitSlop={8}>
        <Text style={styles.link}>Fix Settings →</Text>
      </Pressable>
    </Banner>
  );
}

const styles = StyleSheet.create({
  message: { fontFamily: Font.text, fontSize: 14, color: Status.cancelled.text },
  link: { fontFamily: Font.textSemiBold, fontSize: 14, color: Status.cancelled.text },
});