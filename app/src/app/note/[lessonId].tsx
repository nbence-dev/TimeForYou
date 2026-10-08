import { useLocalSearchParams } from 'expo-router';

import { NoteEditScreen } from '@/features/notes/note-edit-screen';

export default function NoteRoute() {
  const { lessonId, noteId } = useLocalSearchParams<{ lessonId: string; noteId?: string }>();
  // Keyed so a different lesson or note starts from a clean editor.
  return <NoteEditScreen key={`${lessonId}/${noteId ?? 'new'}`} lessonId={lessonId} noteId={noteId} />;
}
