// Mirrors docs/openapi.yaml. Field names match the wire format (snake_case).

import type { LessonMode } from '@/shared/types/lesson';

export type { LessonMode };

export type LessonStatus = 'scheduled' | 'cancelled' | 'irregular';
export type ChangeType = 'cancelled' | 'room' | 'time' | 'added' | 'removed';
export type DevicePlatform = 'android_fcm' | 'web_push';

export interface QrScanRequest {
  server: string;
  school: string;
  user: string;
  key: string;
}

export interface QrScanResponse {
  token: string;
  user_id: string;
}

export interface Lesson {
  id: string;
  untis_lesson_id: string;
  subject: string;
  lecturer?: string | null;
  room: string | null;
  group?: string | null;
  mode?: LessonMode | null;
  start_ts: string;
  end_ts: string;
  status: LessonStatus;
}

export interface ChangeLogEntry {
  id: string;
  lesson_id: string;
  change_type: ChangeType;
  old_value?: string | null;
  new_value?: string | null;
  detected_at: string;
}

export interface CalendarFeed {
  url: string;
}

export interface Note {
  id: string;
  lesson_id: string;
  body: string;
  created_at: string;
  updated_at: string;
}

export interface Device {
  id: string;
  platform: DevicePlatform;
  push_token: string;
  created_at: string;
}

export interface DeviceRegisterRequest {
  platform: DevicePlatform;
  push_token: string;
}

export interface UserSettings {
  change_alerts_enabled: boolean;
}