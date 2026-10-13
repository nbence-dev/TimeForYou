import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Button } from '@/shared/components/button';
import { ScreenHeader } from '@/shared/components/screen-header';
import { loadLessonNotes, saveNoteChange } from '@/shared/sync/notes-outbox';
import { announceSynced } from '@/shared/sync/synced';
import { Font } from '@/shared/theme/fonts';
import { Brand, Colors, MaxContentWidth, Radius, Spacing, Status } from '@/shared/theme/theme';

import {
  checkNoteText,
  formatLessonWhen,
  loadErrorMessage,
  MAX_NOTE_LENGTH,
  noteBodyToSend,
  noteErrorMessage,
} from './note-rules';
import { useCachedLesson } from './use-cached-lesson';

/**
 * Write a new note on a lesson, or edit or delete an existing one (#73). With no connection the
 * change is kept on the device and sent later (#124), so the screen still closes and no text is lost.
 * A failure that is not about the connection keeps what the student typed and says what went wrong.
 */
export function NoteEditScreen({ lessonId, noteId }: { lessonId: string; noteId?: string }) {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const lesson = useCachedLesson(lessonId);

  const [text, setText] = useState('');
  // A new note has nothing to load; an existing one is loaded (server copy, or the saved copy offline).
  const [loaded, setLoaded] = useState(noteId === undefined);
  const [missing, setMissing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const close = () => (router.canGoBack() ? router.back() : router.replace('/'));

  // `saving` only disables the buttons after the next render, so two quick taps could both get
  // through. This flips immediately and stops a second request being sent.
  const requestInFlight = useRef(false);

  useEffect(() => {
    if (noteId === undefined) return;
    let active = true;
    loadLessonNotes(lessonId).then(
      ({ notes }) => {
        if (!active) return;
        const found = notes.find((n) => n.id === noteId);
        if (found) setText(found.body);
        else setMissing(true);
        setLoadError(null);
        setLoaded(true);
      },
      (failure: unknown) => {
        if (!active) return;
        setLoadError(loadErrorMessage(failure));
        setLoaded(true);
      },
    );
    return () => {
      active = false;
    };
  }, [lessonId, noteId, attempt]);

  async function save() {
    if (requestInFlight.current) return;
    const problem = checkNoteText(text);
    if (problem) {
      setError(problem);
      return;
    }
    requestInFlight.current = true;
    setSaving(true);
    setError(null);
    try {
      const body = noteBodyToSend(text);
      const result = noteId
        ? await saveNoteChange({ kind: 'update', id: noteId, lessonId, body })
        : await saveNoteChange({ kind: 'create', lessonId, body });
      // Only refetch the timetable when the server really has it; a queued change syncs later.
      if (result === 'sent') announceSynced();
      close();
    } catch (failure) {
      // Stay on this screen with the text intact, and let the student try again.
      setError(noteErrorMessage(failure));
      setSaving(false);
      requestInFlight.current = false;
    }
  }

  async function remove() {
    if (!noteId || requestInFlight.current) return;
    requestInFlight.current = true;
    setSaving(true);
    setError(null);
    try {
      const result = await saveNoteChange({ kind: 'delete', id: noteId, lessonId });
      if (result === 'sent') announceSynced();
      close();
    } catch (failure) {
      setError(noteErrorMessage(failure));
      setConfirmingDelete(false);
      setSaving(false);
      requestInFlight.current = false;
    }
  }

  const retryLoad = () => {
    setLoaded(false);
    setAttempt((n) => n + 1);
  };

  return (
    <ScrollView
      style={styles.screen}
      keyboardShouldPersistTaps="handled"
      // iOS: lift the buttons above the keyboard instead of leaving them underneath it.
      automaticallyAdjustKeyboardInsets
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + Spacing.four }]}>
      <View style={styles.column}>
        <ScreenHeader title={noteId ? 'Edit note' : 'New note'} onClose={close} />

        {lesson ? (
          <View style={styles.lessonCard}>
            <Text style={styles.lessonTitle}>{lesson.subject}</Text>
            <Text style={styles.lessonMeta}>
              {formatLessonWhen(lesson.start_ts, lesson.end_ts)}
              {lesson.room ? ` · ${lesson.room}` : ''}
            </Text>
          </View>
        ) : null}

        {!loaded ? (
          <Text style={styles.status}>Loading note…</Text>
        ) : missing ? (
          <View style={styles.notice} accessibilityRole="alert">
            <Text style={styles.noticeText}>
              This note is no longer available. It may have been deleted on another device.
            </Text>
            <Button label="Close" variant="secondary" onPress={close} />
          </View>
        ) : loadError ? (
          <View style={styles.notice} accessibilityRole="alert">
            <Text style={styles.noticeText}>{loadError}</Text>
            <Button label="Try again" variant="secondary" onPress={retryLoad} />
          </View>
        ) : (
          <>
            <View style={styles.inputBox}>
              <TextInput
                accessibilityLabel="Note text"
                style={styles.input}
                value={text}
                onChangeText={(next) => {
                  setText(next);
                  setError(null);
                }}
                placeholder="Write a note, e.g. bring laptop charger"
                placeholderTextColor={Colors.light.textSecondary}
                multiline
                autoFocus
                maxLength={MAX_NOTE_LENGTH}
                editable={!saving}
                textAlignVertical="top"
              />
              <View style={styles.inputFooter}>
                <View style={styles.privateRow}>
                  <Ionicons name="lock-closed-outline" size={14} color={Colors.light.textSecondary} />
                  <Text style={styles.privateText}>Private note · only visible to you</Text>
                </View>
                <Text style={styles.counter} accessibilityLabel={`${text.length} of ${MAX_NOTE_LENGTH} characters`}>
                  {text.length} / {MAX_NOTE_LENGTH}
                </Text>
              </View>
            </View>

            {error ? (
              <Text style={styles.errorText} accessibilityRole="alert">
                {error}
              </Text>
            ) : null}

            <View style={styles.actions}>
              <Button label={saving ? 'Saving…' : 'Save note'} disabled={saving} onPress={() => void save()} />
              {noteId ? (
                confirmingDelete ? (
                  <View style={styles.confirm} accessibilityRole="alert">
                    <Text style={styles.confirmText}>Delete this note?</Text>
                    <View style={styles.confirmButtons}>
                      <View style={styles.flex}>
                        <Button label="Delete" variant="destructive" disabled={saving} onPress={() => void remove()} />
                      </View>
                      <View style={styles.flex}>
                        <Button label="Cancel" variant="secondary" disabled={saving} onPress={() => setConfirmingDelete(false)} />
                      </View>
                    </View>
                  </View>
                ) : (
                  <Button label="Delete note" variant="destructive" disabled={saving} onPress={() => setConfirmingDelete(true)} />
                )
              ) : null}
            </View>
          </>
        )}
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: Colors.light.background },
  content: { paddingHorizontal: Spacing.three, alignItems: 'center' },
  column: { width: '100%', maxWidth: MaxContentWidth, gap: Spacing.three },
  lessonCard: {
    gap: Spacing.one,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  lessonTitle: { fontFamily: Font.textBold, fontSize: 18, color: Brand.navy },
  lessonMeta: { fontFamily: Font.label, fontSize: 13, color: Colors.light.textSecondary },
  inputBox: {
    padding: Spacing.three,
    gap: Spacing.two,
    borderRadius: Radius.card,
    borderWidth: 2,
    borderColor: Brand.orange,
    backgroundColor: Brand.white,
  },
  input: { minHeight: 140, fontFamily: Font.text, fontSize: 16, color: Colors.light.text },
  inputFooter: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: Spacing.two,
    borderTopWidth: 1,
    borderTopColor: Brand.border,
  },
  privateRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.one },
  privateText: { fontFamily: Font.label, fontSize: 12, color: Colors.light.textSecondary },
  counter: { fontFamily: Font.label, fontSize: 12, color: Colors.light.textSecondary, fontVariant: ['tabular-nums'] },
  errorText: { fontFamily: Font.text, fontSize: 14, color: Status.cancelled.text },
  actions: { gap: Spacing.two, marginTop: Spacing.two },
  confirm: { gap: Spacing.two },
  confirmText: { fontFamily: Font.textSemiBold, fontSize: 14, color: Status.cancelled.text, textAlign: 'center' },
  confirmButtons: { flexDirection: 'row', gap: Spacing.two },
  flex: { flex: 1 },
  status: { fontFamily: Font.text, fontSize: 16, color: Colors.light.textSecondary, paddingVertical: Spacing.three },
  notice: {
    gap: Spacing.three,
    padding: Spacing.three,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: Brand.border,
    backgroundColor: Brand.white,
  },
  noticeText: { fontFamily: Font.text, fontSize: 14, color: Colors.light.text },
});