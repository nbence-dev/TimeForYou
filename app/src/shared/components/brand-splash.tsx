import * as SplashScreen from 'expo-splash-screen';
import { useEffect, useState } from 'react';
import { Animated, Image, StyleSheet } from 'react-native';

/** Shortest time the branded screen stays up, so it reads as a screen rather than a flicker. */
const MIN_VISIBLE_MS = 1200;
const FADE_MS = 300;

/** The navy of `splash-screen.png`; also the native splash background in `app.json`. */
const SPLASH_NAVY = '#15315D';

type BrandSplashProps = {
  /** True once fonts and the stored session have resolved. */
  ready: boolean;
};

/**
 * Full-screen branded splash shown after the native one. It covers the app from first paint,
 * releases the native splash once it is on screen, then fades out when `ready` and the minimum
 * display time have both passed.
 */
export function BrandSplash({ ready }: BrandSplashProps) {
  const [opacity] = useState(() => new Animated.Value(1));
  const [minElapsed, setMinElapsed] = useState(false);
  const [gone, setGone] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setMinElapsed(true), MIN_VISIBLE_MS);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    if (!ready || !minElapsed) return;
    Animated.timing(opacity, { toValue: 0, duration: FADE_MS, useNativeDriver: true }).start(() =>
      setGone(true),
    );
  }, [ready, minElapsed, opacity]);

  if (gone) return null;

  return (
    <Animated.View
      pointerEvents="none"
      style={[styles.root, { opacity }]}
      // The native splash is only released once this is laid out, so there is no blank frame.
      onLayout={() => SplashScreen.hideAsync().catch(() => {})}>
      <Image
        source={require('../../../assets/images/splash-screen.png')}
        style={styles.image}
        resizeMode="cover"
        accessibilityLabel="TimeForYou"
      />
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  root: { ...StyleSheet.absoluteFill, backgroundColor: SPLASH_NAVY },
  image: { width: '100%', height: '100%' },
});
