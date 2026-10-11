import { Ionicons } from '@expo/vector-icons';
import * as Clipboard from 'expo-clipboard';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api, NetworkError, UnauthorizedError } from '@/shared/api';
import { Button } from '@/shared/components/button';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';

import { Row, Section, SoonTag } from './settings-rows';

/** How long "Copied" stays on the button before it says "Copy link" again. */
const COPIED_MS = 2000;

/**
 * The calendar subscription screen (#109): the student's own feed URL from `GET /calendar`, with
 * Copy link. Reset link and the one-tap buttons have no backing yet (#112), so they are disabled and
 * tagged "Soon".
 */
export function CalendarScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const [url, setUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [problem, setProblem] = useState<'offline' | 'failed' | null>(null);
  const [copied, setCopied] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);
  const mounted = useRef(true);
  // A 401 signed the student out: keep showing "Loading" instead of a misleading error.
  const signedOut = useRef(false);
  const copiedTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // State is only set after the request settles. Never rejects.
  const load = useCallback(
    () =>
      api
        .getCalendar()
        .then((feed) => {
          if (!mounted.current) return;
          setUrl(feed.url);
          setProblem(null);
        })
        .catch((error: unknown) => {
          // A 401 has already signed the student out; the app is on its way to the scan screen.
          if (error instanceof UnauthorizedError) {
            signedOut.current = true;
            return;
          }
          if (mounted.current) setProblem(error instanceof NetworkError ? 'offline' : 'failed');
        })
        .finally(() => {
          if (mounted.current && !signedOut.current) setLoading(false);
        }),
    [],
  );

  useEffect(() => {
    mounted.current = true;
    void load();
    return () => {
      mounted.current = false;
      if (copiedTimer.current) clearTimeout(copiedTimer.current);
    };
  }, [load]);

  function retry() {
    setLoading(true);
    void load();
  }

  async function copy() {
    if (!url) return;
    setCopyFailed(false);
    // On the web a refused copy resolves to false instead of throwing.
    const ok = await Clipboard.setStringAsync(url).catch(() => false);
    if (!mounted.current) return;
    if (!ok) {
      setCopyFailed(true);
      return;
    }
    setCopied(true);
    if (copiedTimer.current) clearTimeout(copiedTimer.current);
    copiedTimer.current = setTimeout(() => setCopied(false), COPIED_MS);
  }

  const close = () => (router.canGoBack() ? router.back() : router.replace('/settings'));

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + Spacing.three }]}>
      <View style={styles.column}>
        <View style={styles.header}>
          <Pressable accessibilityRole="button" accessibilityLabel="Back" hitSlop={8} onPress={close} style={styles.back}>
            <Ionicons name="arrow-back" size={22} color={Brand.navy} />
          </Pressable>
          <Text style={styles.title} accessibilityRole="header">
            Calendar subscription
          </Text>
        </View>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Live calendar sync</Text>
          <Text style={styles.body}>
            See your timetable next to your other events. Future lessons only, with your notes in the event
            description.
          </Text>
        </View>

        <View style={styles.card}>
          <Text style={styles.label}>YOUR PRIVATE FEED</Text>
          {loading ? (
            <Text style={styles.body}>Loading your link…</Text>
          ) : url ? (
            <>
              <View style={styles.urlBox}>
                <Text style={styles.url} selectable>
                  {url}
                </Text>
              </View>
              <Button label={copied ? 'Copied' : 'Copy link'} onPress={copy} />
              {copyFailed ? (
                <Text style={styles.error} accessibilityRole="alert">
                  Couldn&apos;t copy. Select the link above and copy it by hand.
                </Text>
              ) : null}
            </>
          ) : (
            <View style={styles.notice} accessibilityRole="alert">
              <Text style={styles.noticeText}>
                {problem === 'offline'
                  ? "You're offline. Connect to the internet to see your calendar link."
                  : "Couldn't load your calendar link. Try again in a moment."}
              </Text>
              <Button label="Try again" variant="secondary" onPress={retry} />
            </View>
          )}
        </View>

        <Section label="Quick one-tap subscription">
          <Row icon="logo-apple" title="Add to Apple Calendar" soon />
          <Row icon="logo-google" title="Add to Google Calendar" soon />
        </Section>

        <View style={styles.warning}>
          <Ionicons name="lock-closed-outline" size={20} color={Brand.navy} />
          <Text style={styles.warningText}>Anyone with this link can see your timetable. Don&apos;t share it.</Text>
        </View>

        <View style={styles.reset}>
          <View style={styles.soonRow}>
            <Text style={styles.resetTitle}>Reset link</Text>
            <SoonTag />
          </View>
          <Text style={styles.muted}>Resetting will revoke the current URL and stop existing calendar sync.</Text>
        </View>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, paddingBottom: Spacing.four, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },

  header: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  back: {
    width: 44,
    height: 44,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: { flex: 1, fontFamily: Font.textBold, fontSize: 22, color: Brand.navy },

  card: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  cardTitle: { fontFamily: Font.textBold, fontSize: 18, color: Brand.navy },
  label: { fontFamily: Font.labelSemiBold, fontSize: 12, letterSpacing: 1, color: Colors.light.textSecondary },
  body: { fontFamily: Font.text, fontSize: 14, lineHeight: 20, color: Colors.light.text },
  urlBox: {
    padding: Spacing.three,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
  },
  url: { fontFamily: Font.label, fontSize: 13, color: Brand.navy },
  error: { fontFamily: Font.textSemiBold, fontSize: 14, color: Status.cancelled.text },
  notice: { gap: Spacing.three },
  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },

  warning: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    backgroundColor: Colors.light.backgroundElement,
  },
  warningText: { flex: 1, fontFamily: Font.text, fontSize: 14, color: Brand.navy },

  reset: { alignItems: 'center', gap: Spacing.two, opacity: 0.55 },
  soonRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  resetTitle: { fontFamily: Font.textSemiBold, fontSize: 16, color: Status.cancelled.text },
  muted: { fontFamily: Font.text, fontSize: 13, color: Colors.light.textSecondary, textAlign: 'center' },
});
