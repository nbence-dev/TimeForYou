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
  has_note?: boolean;
}

export interface ChangeLogEntry {
  id: string;
  lesson_id: string;
  change_type: ChangeType;
  old_value?: string | null;
  new_value?: string | null;
  detected_at: string;
}

/** One of the student's own devices, from `GET /devices`. Never carries the push token. */
export interface LinkedDevice {
  id: string;
  /** `null` until the device first registers for push. */
  platform: DevicePlatform | null;
  notifications_on: boolean;
  created_at: string;
  last_seen_at: string;
  /** True for the device making the request. */
  is_current: boolean;
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

export interface Profile {
  /** Null until the server's first poll has fetched it. */
  display_name: string | null;
  /** Null when WebUntis has no class for the student. */
  class_name: string | null;
}