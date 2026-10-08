import { useLocalSearchParams } from 'expo-router';

import { LessonDetailScreen } from '@/features/notes/lesson-detail-screen';

export default function LessonRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  // Keyed by id so a different lesson never inherits the previous lesson's notes.
  return <LessonDetailScreen key={id} lessonId={id} />;
}
