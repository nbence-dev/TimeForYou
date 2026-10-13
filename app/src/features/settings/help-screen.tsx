import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing } from '@/shared/theme/theme';

/** What the Help and FAQ screen says (#113). Plain text, so the team can change the wording here. */
const FAQ: readonly { question: string; answer: string }[] = [
  {
    question: 'What does TimeForYou do?',
    answer:
      'It keeps a copy of your WebUntis timetable on your phone and tells you when a lesson is cancelled, moves to another room or changes time.',
  },
  {
    question: 'How often does it check for changes?',
    answer:
      'Every 10 minutes. To refresh what the app shows straight away, pull down on Today or Timetable, or tap Sync now in Settings.',
  },
  {
    question: "Why didn't I get a notification?",
    answer:
      'Check that Change alerts is on in Settings and that notifications are allowed for TimeForYou on your phone. On an iPhone, add TimeForYou to your Home Screen first: iOS only sends alerts to the Home Screen app. Every change still shows in the Changes tab.',
  },
  {
    question: 'Where can I see what changed?',
    answer:
      'In the Changes tab, newest first, with the old and new room or time. Changes from the last 30 days are kept.',
  },
  {
    question: 'Does it work offline?',
    answer:
      "Yes. You see the timetable from the last time it synced. New changes show up once you're back online.",
  },
  {
    question: 'Can I use it on more than one device?',
    answer:
      'Yes. Scan your WebUntis QR code on each device. Your notes and the Change alerts setting are shared between them.',
  },
  {
    question: 'Can I see my timetable in my calendar app?',
    answer:
      'Yes. Open Calendar subscription in Settings and copy your link into Google Calendar or Apple Calendar. Keep the link private: anyone with it can see your timetable.',
  },
  {
    question: 'Where does my name come from?',
    answer: 'From WebUntis. If it is wrong, it needs to be fixed in WebUntis; the app updates on its next sync.',
  },
  {
    question: 'How do I sign out?',
    answer:
      'Tap Unlink this device at the bottom of Settings. Your notes stay saved, and you can scan your QR code again to come back.',
  },
];

/** Help and FAQ (#113): static questions and answers, opened from Settings. */
export function HelpScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
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
            Help and FAQ
          </Text>
        </View>

        {FAQ.map(({ question, answer }) => (
          <View key={question} style={styles.card}>
            <Text style={styles.question}>{question}</Text>
            <Text style={styles.answer}>{answer}</Text>
          </View>
        ))}
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
    gap: Spacing.two,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  question: { fontFamily: Font.textSemiBold, fontSize: 16, color: Brand.navy },
  answer: { fontFamily: Font.text, fontSize: 14, lineHeight: 20, color: Colors.light.text },
});