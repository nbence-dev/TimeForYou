import type { CardStatus } from '@/shared/theme/theme';
import type { LessonStatus } from '@/shared/types/lesson';

/**
 * Maps the API's lesson status (what `GET /timetable` and the offline cache carry) onto the lesson
 * card's visual states. The two vocabularies differ on purpose: the card also has `live`, which is
 * worked out from the clock rather than sent by the server, so this never returns it.
 *
 * Anything else, including a status the server adds later, shows as a normal lesson rather than
 * breaking the card.
 */
export function toCardStatus(status: LessonStatus): CardStatus {
  switch (status) {
    case 'cancelled':
      return 'cancelled';
    case 'irregular':
      return 'changed';
    default:
      return 'onTime';
  }
}
