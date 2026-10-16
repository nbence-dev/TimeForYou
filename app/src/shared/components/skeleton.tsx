import { useEffect, useState } from 'react';
import { Animated, Platform, StyleSheet, Text, View } from 'react-native';

import { Font } from '@/shared/theme/fonts';

import { Button } from './button';
import { Brand, Colors, Radius, Spacing } from '@/shared/theme/theme';

/** After this long a first load is slower than it should be, so the screen says so. */
const SLOW_AFTER_MS = 6000;

/**
 * What a first load looks like while there is nothing saved to show: lesson-card-shaped rows with a
 * simple shimmer, and after a few seconds a line saying it is taking longer than usual, with a Try
 * again button when `onRetry` is given. Mount it only while loading; it goes away with the data. A
 * screen that has saved data keeps showing it and never uses this.
 */
export function LoadingSkeleton({
  label,
  count = 3,
  onRetry,
}: {
  label: string;
  count?: number;
  onRetry?: () => void;
}) {
  const [pulse] = useState(() => new Animated.Value(0.45));
  const [slow, setSlow] = useState(false);

  useEffect(() => {
    // The native driver is not available on the web.
    const useNativeDriver = Platform.OS !== 'web';
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { toValue: 1, duration: 800, useNativeDriver }),
        Animated.timing(pulse, { toValue: 0.45, duration: 800, useNativeDriver }),
      ]),
    );
    loop.start();
    const timer = setTimeout(() => setSlow(true), SLOW_AFTER_MS);
    return () => {
      loop.stop();
      clearTimeout(timer);
    };
  }, [pulse]);

  return (
    <View style={styles.wrap}>
      <View
        accessibilityLabel={label}
        accessibilityState={{ busy: true }}
        aria-busy
        accessibilityLiveRegion="polite"
        style={styles.wrap}>
        {Array.from({ length: count }, (_, i) => (
          <Animated.View key={i} style={[styles.card, { opacity: pulse }]}>
            <View style={styles.strip} />
            <View style={styles.body}>
              <View style={[styles.bar, { width: '38%' }]} />
              <View style={[styles.bar, styles.barTall, { width: '72%' }]} />
              <View style={[styles.bar, { width: '55%' }]} />
            </View>
          </Animated.View>
        ))}
      </View>
      {slow ? (
        <>
          <Text style={styles.slow} accessibilityRole="alert">
            Taking longer than usual. Check your connection; this will appear as soon as it loads.
          </Text>
          {onRetry ? <Button label="Try again" variant="secondary" onPress={onRetry} /> : null}
        </>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { gap: Spacing.three },
  card: {
    flexDirection: 'row',
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
    overflow: 'hidden',
  },
  strip: { width: 4, backgroundColor: Colors.light.backgroundSelected },
  body: { flex: 1, gap: Spacing.two, padding: Spacing.three },
  bar: { height: 12, borderRadius: Radius.base, backgroundColor: Colors.light.backgroundSelected },
  barTall: { height: 16 },
  slow: { fontFamily: Font.label, fontSize: 13, lineHeight: 18, color: Colors.light.textSecondary, textAlign: 'center' },
});
