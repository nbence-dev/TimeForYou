import type { QrScanRequest } from '@/shared/api';

const FIELDS = ['server', 'school', 'user', 'key'] as const;

/**
 * Reads a scanned QR string as the WebUntis mobile-access payload: a JSON object carrying
 * `server`, `school`, `user` and `key` (what the mock's `/qr-code` encodes). Returns `null` for
 * anything else, so the caller can show "that QR code didn't work" without a network round trip.
 */
export function parseQrPayload(raw: string): QrScanRequest | null {
  let data: unknown;
  try {
    data = JSON.parse(raw);
  } catch {
    return null;
  }
  if (typeof data !== 'object' || data === null) return null;
  const record = data as Record<string, unknown>;
  const out: Partial<QrScanRequest> = {};
  for (const field of FIELDS) {
    const value = record[field];
    if (typeof value !== 'string' || value.trim() === '') return null;
    out[field] = value;
  }
  return out as QrScanRequest;
}
