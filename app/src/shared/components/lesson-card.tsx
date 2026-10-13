import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import type { LessonMode } from '@/shared/types/lesson';
import { Brand, Colors, Radius, Spacing, Status, type CardStatus } from '@/shared/theme/theme';
import { Font } from '@/shared/theme/fonts';

export type LessonCardProps = {
  /** `onTime` is the normal state. A lesson from the API goes through `toCardStatus` first. */
  status?: CardStatus;
  /** Display times, e.g. "08:00" and "09:30". */
  start: string;
  end: string;
  /** Omit when there is none: the API's lesson carries a subject name but no course code. */
  courseCode?: string;
  title: string;
  room?: string | null;
  teacher?: string | null;
  /** Display-only extras; neither is ever a notification trigger. */
  group?: string | null;
  mode?: LessonMode | null;
  hasNote?: boolean;
  /** A change the student has not looked at yet: shows the orange dot. */
  unseen?: boolean;
  onPress?: () => void;
};

export function LessonCard({
  status = 'onTime',
  start,
  end,
  courseCode,
  title,
  room,
  teacher,
  group,
  mode,
  hasNote,
  unseen,
  onPress,
}: LessonCardProps) {
  const cancelled = status === 'cancelled';
  // The fallback is for a status that slipped past the types (a cast, or JS callers): render a
  // normal card instead of throwing on `colors.strip`.
  const colors = Status[status] ?? Status.onTime;
  const metaColor = cancelled ? Status.cancelled.text : Colors.light.textSecondary;

  return (
    <Pressable
      accessibilityRole={onPress ? 'button' : undefined}
      accessibilityLabel={`${title}, ${start} to ${end}${cancelled ? ', cancelled' : ''}`}
      onPress={onPress}
      style={[styles.card, cancelled && styles.cancelledCard]}>
      <View style={[styles.strip, { backgroundColor: colors.strip }]} />
      <View style={styles.body}>
        <View style={styles.row}>
          <Text style={[styles.time, cancelled && { color: Status.cancelled.strip }]}>
            {start} – {end}
          </Text>
          {courseCode ? (
            <View style={styles.code}>
              <Text style={styles.codeText}>{courseCode}</Text>
            </View>
          ) : null}
          <View style={styles.flex} />
          {hasNote ? (
            <Ionicons
              name="document-text"
              size={18}
              color={Brand.orange}
              accessibilityLabel="Has a note"
            />
          ) : null}
          {unseen ? <View accessibilityLabel="Unseen change" style={styles.dot} /> : null}
        </View>
        <Text style={[styles.title, cancelled && styles.titleCancelled]} numberOfLines={2}>
          {title}
        </Text>
        <View style={styles.row}>
          {room ? <Meta icon="location-outline" text={room} color={metaColor} /> : null}
          {teacher ? <Meta icon="person-outline" text={teacher} color={metaColor} /> : null}
        </View>
        {group || mode ? (
          <View style={styles.row}>
            {group ? <Meta icon="people-outline" text={group} color={metaColor} /> : null}
            {mode === 'online' || mode === 'in_person' ? (
              <Meta
                icon={mode === 'online' ? 'videocam-outline' : 'business-outline'}
                text={mode === 'online' ? 'Online' : 'In person'}
                color={metaColor}
              />
            ) : null}
          </View>
        ) : null}
      </View>
    </Pressable>
  );
}

type MetaIcon = 'location-outline' | 'person-outline' | 'people-outline' | 'videocam-outline' | 'business-outline';

function Meta({ icon, text, color }: { icon: MetaIcon; text: string; color: string }) {
  return (
    <View style={styles.meta}>
      <Ionicons name={icon} size={14} color={color} />
      <Text style={[styles.metaText, { color }]}>{text}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    flexDirection: 'row',
    backgroundColor: Brand.white,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    overflow: 'hidden',
  },
  cancelledCard: { backgroundColor: Status.cancelled.surface, borderColor: Status.cancelled.border },
  strip: { width: 4 },
  body: { flex: 1, padding: Spacing.three, gap: Spacing.two },
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  flex: { flex: 1 },
  time: {
    fontFamily: Font.label,
    fontSize: 14,
    color: Colors.light.textSecondary,
    fontVariant: ['tabular-nums'],
  },
  code: {
    backgroundColor: Colors.light.backgroundElement,
    borderRadius: Radius.base,
    paddingHorizontal: Spacing.two,
    paddingVertical: Spacing.half,
  },
  codeText: { fontFamily: Font.labelSemiBold, fontSize: 12, color: Brand.navy },
  title: { fontFamily: Font.textBold, fontSize: 18, color: Colors.light.text },
  titleCancelled: { textDecorationLine: 'line-through', color: Status.cancelled.text },
  meta: { flexDirection: 'row', alignItems: 'center', gap: Spacing.one },
  metaText: { fontFamily: Font.label, fontSize: 13 },
  dot: { width: 8, height: 8, borderRadius: Radius.pill, backgroundColor: Brand.orange },
});
