import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ApiError } from '@/shared/api';
import { Button } from '@/shared/components/button';
import { ScreenHeader } from '@/shared/components/screen-header';
import { StatusChip } from '@/shared/components/status-chip';
import { saveNoteChange, type ShownNote } from '@/shared/sync/notes-outbox';
import { announceSynced } from '@/shared/sync/synced';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';

import { formatLessonWhen, formatNoteAge, noteErrorMessage } from './note-rules';
import { useCachedLesson } from './use-cached-lesson';
import { useLessonNotes } from './use-lesson-notes';

/**
 * A lesson and the student's notes on it: view, add, edit and delete (#73). Opens over the timetable.
 * Works offline (#124): changes made without a connection show at once, marked "Waiting to sync".
 */
export function LessonDetailScreen({ lessonId }: { lessonId: string }) {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const lesson = useCachedLesson(lessonId);
  const { notes, offline, error, reload } = useLessonNotes(lessonId);

  const [confirming, setConfirming] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const close = () => (router.canGoBack() ? router.back() : router.replace('/'));
  const edit = (noteId?: string) => {
    const lesson = encodeURIComponent(lessonId);
    router.push(noteId ? `/note/${lesson}?noteId=${encodeURIComponent(noteId)}` : `/note/${lesson}`);
  };

  // Set straight away, unlike `deleting`, so a second tap before the next render sends nothing.
  const deleteInFlight = useRef(false);

  async function deleteNote(noteId: string) {
    if (deleteInFlight.current) return;
    deleteInFlight.current = true;
    setDeleting(true);
    setDeleteError(null);
    try {
      // Offline, the delete is kept and sent later; the note disappears from the list either way.
      const result = await saveNoteChange({ kind: 'delete', id: noteId, lessonId });
      if (result === 'sent') announceSynced(); // the card's note icon comes from the timetable
      setConfirming(null);
      await reload();
    } catch (failure) {
      setDeleteError(noteErrorMessage(failure));
      if (failure instanceof ApiError && failure.status === 404) {
        // Already deleted (on another device): drop the prompt and show what is really there.
        setConfirming(null);
        await reload();
      }
    } finally {
      deleteInFlight.current = false;
      setDeleting(false);
    }
  }

  const now = new Date();

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + Spacing.four }]}>
      <View style={styles.column}>
        <ScreenHeader
          title={lesson ? lesson.subject : 'Lesson'}
          onClose={close}
          above={lesson?.status === 'cancelled' ? <StatusChip kind="cancelled" /> : undefined}
          below={
            lesson ? (
              <Text style={styles.meta}>
                {formatLessonWhen(lesson.start_ts, lesson.end_ts)}
                {lesson.room ? ` · ${lesson.room}` : ''}
                {lesson.lecturer ? ` · ${lesson.lecturer}` : ''}
              </Text>
            ) : lesson === null ? (
              // Looked up and not there (the timetable moved on since this sheet was opened).
              <Text style={styles.meta}>This lesson is no longer on your timetable.</Text>
            ) : undefined
          }
        />

        <View style={styles.sectionHeading}>
          <Ionicons name="document-text-outline" size={18} color={Brand.navy} />
          <Text style={styles.sectionLabel}>MY NOTES</Text>
        </View>

        {offline && notes ? (
          <Text style={styles.offlineText}>
            You&apos;re offline. Showing the notes saved on this device; changes sync when you&apos;re back online.
          </Text>
        ) : null}

        {error ? (
          <View style={styles.notice} accessibilityRole="alert">
            <Text style={styles.noticeText}>{error}</Text>
            <Button label="Try again" variant="secondary" onPress={() => void reload()} />
          </View>
        ) : notes === null ? (
          <Text style={styles.empty}>Loading notes…</Text>
        ) : notes.length === 0 ? (
          <Text style={styles.empty}>No notes on this lesson yet.</Text>
        ) : (
          <View style={styles.list}>
            {notes.map((note) => (
              <NoteCard
                key={note.id}
                note={note}
                now={now}
                confirming={confirming === note.id}
                busy={deleting}
                onEdit={() => edit(note.id)}
                onAskDelete={() => {
                  setDeleteError(null);
                  setConfirming(note.id);
                }}
                onCancelDelete={() => setConfirming(null)}
                onConfirmDelete={() => void deleteNote(note.id)}
              />
            ))}
          </View>
        )}

        {deleteError ? (
          <Text style={styles.errorText} accessibilityRole="alert">
            {deleteError}
          </Text>
        ) : null}

        <View style={styles.actions}>
          <Button label="Add note" onPress={() => edit()} />
          <Button label="Close" variant="secondary" onPress={close} />
        </View>
      </View>
    </ScrollView>
  );
}

function NoteCard({
  note,
  now,
  confirming,
  busy,
  onEdit,
  onAskDelete,
  onCancelDelete,
  onConfirmDelete,
}: {
  note: ShownNote;
  now: Date;
  confirming: boolean;
  busy: boolean;
  onEdit: () => void;
  onAskDelete: () => void;
  onCancelDelete: () => void;
  onConfirmDelete: () => void;
}) {
  const age = formatNoteAge(note.created_at, now);
  return (
    <View style={styles.card}>
      <View style={styles.cardRow}>
        <Text style={styles.noteBody}>{note.body}</Text>
        <View style={styles.cardIcons}>
          <Pressable accessibilityRole="button" accessibilityLabel="Edit note" hitSlop={8} onPress={onEdit}>
            <Ionicons name="create-outline" size={20} color={Colors.light.textSecondary} />
          </Pressable>
          <Pressable accessibilityRole="button" accessibilityLabel="Delete note" hitSlop={8} onPress={onAskDelete}>
            <Ionicons name="trash-outline" size={20} color={Colors.light.textSecondary} />
          </Pressable>
        </View>
      </View>
      {note.pending ? (
        <View style={styles.pendingRow}>
          <Ionicons name="cloud-upload-outline" size={14} color={Status.changed.text} />
          <Text style={styles.pendingText}>Waiting to sync</Text>
        </View>
      ) : (
        <Text style={styles.noteMeta}>
          {age ? `Created ${age} · ` : ''}Private to you
        </Text>
      )}
      {confirming ? (
        <View style={styles.confirmRow} accessibilityRole="alert">
          <Text style={styles.confirmText}>Delete this note?</Text>
          <View style={styles.confirmButtons}>
            <View style={styles.flex}>
              <Button label="Delete" variant="destructive" disabled={busy} onPress={onConfirmDelete} />
            </View>
            <View style={styles.flex}>
              <Button label="Cancel" variant="secondary" disabled={busy} onPress={onCancelDelete} />
            </View>
          </View>
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  meta: { fontFamily: Font.label, fontSize: 14, color: Colors.light.textSecondary },
  sectionHeading: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two, marginTop: Spacing.two },
  sectionLabel: { fontFamily: Font.textBold, fontSize: 14, letterSpacing: 0.5, color: Brand.navy },
  offlineText: { fontFamily: Font.text, fontSize: 13, color: Colors.light.textSecondary },
  list: { gap: Spacing.three },
  card: {
    gap: Spacing.two,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  cardRow: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.three },
  noteBody: { flex: 1, fontFamily: Font.text, fontSize: 16, color: Colors.light.text },
  cardIcons: { flexDirection: 'row', gap: Spacing.three },
  noteMeta: { fontFamily: Font.label, fontSize: 12, color: Colors.light.textSecondary },
  pendingRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.one },
  pendingText: { fontFamily: Font.labelSemiBold, fontSize: 12, color: Status.changed.text },
  confirmRow: { gap: Spacing.two, paddingTop: Spacing.two },
  confirmText: { fontFamily: Font.textSemiBold, fontSize: 14, color: Status.cancelled.text },
  confirmButtons: { flexDirection: 'row', gap: Spacing.two },
  flex: { flex: 1 },
  notice: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },
  empty: { fontFamily: Font.text, fontSize: 16, color: Colors.light.textSecondary, paddingVertical: Spacing.three },
  errorText: { fontFamily: Font.text, fontSize: 14, color: Status.cancelled.text },
  actions: { gap: Spacing.two, marginTop: Spacing.three },
});