import { Ionicons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';

import type { Lesson } from '@/shared/api';
import { StatusChip, type StatusChipKind } from '@/shared/components/status-chip';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, Radius, Spacing, Status } from '@/shared/theme/theme';

import {
  describeValues,
  describeWhen,
  isCancellation,
  type ShownChange,
  type ShownChangeType,
} from './change-format';

const CHIP: Record<ShownChangeType, StatusChipKind> = {
  cancelled: 'cancelled',
  room: 'roomChanged',
  time: 'timeChanged',
};

/**
 * One change: which lesson, what kind of change, and the old and new value side by side.
 * `lesson` is missing when the lesson is no longer in the timetable the app has.
 */
export function ChangeCard({ change, lesson }: { change: ShownChange; lesson?: Lesson }) {
  const cancelled = isCancellation(change);
  // A "cancelled" change that isn't a cancellation is a lesson coming back on.
  const backOn = change.change_type === 'cancelled' && !cancelled;
  const strip = cancelled ? Status.cancelled.strip : backOn ? Status.onTime.strip : Status.changed.strip;
  const metaColor = cancelled ? Status.cancelled.text : Colors.light.textSecondary;

  const when = describeWhen(change, lesson);
  const values = describeValues(change);

  return (
    <View style={[styles.card, cancelled && styles.cancelledCard]}>
      <View style={[styles.strip, { backgroundColor: strip }]} />
      <View style={styles.body}>
        {backOn ? null : <StatusChip kind={CHIP[change.change_type]} />}

        <Text style={[styles.title, cancelled && styles.titleCancelled]} numberOfLines={2}>
          {lesson?.subject ?? 'A lesson in your timetable'}
        </Text>

        {when ? (
          <View style={styles.meta}>
            <Ionicons name="time-outline" size={14} color={metaColor} />
            <Text style={[styles.metaText, { color: metaColor }]}>{when}</Text>
          </View>
        ) : null}

        {values ? (
          <View style={styles.values} accessibilityLabel={`${values.label} changed from ${values.from} to ${values.to}`}>
            <Text style={styles.valueLabel}>{values.label}:</Text>
            <Text style={styles.oldValue}>{values.from}</Text>
            <Ionicons name="arrow-forward" size={14} color={Brand.navy} />
            <View style={styles.newValue}>
              <Text style={styles.newValueText}>{values.to}</Text>
            </View>
          </View>
        ) : null}

        {lesson?.lecturer ? (
          <View style={styles.meta}>
            <Ionicons name="person-outline" size={14} color={metaColor} />
            <Text style={[styles.metaText, { color: metaColor }]}>{lesson.lecturer}</Text>
          </View>
        ) : null}
      </View>
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
  title: { fontFamily: Font.textBold, fontSize: 18, color: Colors.light.text },
  titleCancelled: { textDecorationLine: 'line-through', color: Status.cancelled.text },
  meta: { flexDirection: 'row', alignItems: 'center', gap: Spacing.one },
  metaText: { fontFamily: Font.label, fontSize: 13, fontVariant: ['tabular-nums'] },
  values: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: Spacing.two,
    padding: Spacing.two,
    borderRadius: Radius.base,
    backgroundColor: Colors.light.backgroundElement,
  },
  valueLabel: { fontFamily: Font.label, fontSize: 14, color: Colors.light.textSecondary },
  oldValue: {
    fontFamily: Font.label,
    fontSize: 14,
    color: Colors.light.textSecondary,
    textDecorationLine: 'line-through',
    fontVariant: ['tabular-nums'],
  },
  newValue: {
    backgroundColor: Status.changed.bg,
    borderRadius: Radius.base,
    paddingHorizontal: Spacing.two,
    paddingVertical: Spacing.half,
  },
  newValueText: {
    fontFamily: Font.labelSemiBold,
    fontSize: 14,
    color: Status.changed.text,
    fontVariant: ['tabular-nums'],
  },
});