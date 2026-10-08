/**
 * Where push registration stands on this device.
 * - `unsupported`: no push here (a desktop/Android browser tab, an iOS tab not added to the Home
 *   Screen, or Firebase not configured). Not an error.
 * - `default`: permission not asked yet. `denied`: the student said no. A handled state, not an error.
 * - `registered`: permission granted and the token is stored on the server.
 * - `failed`: permission granted but registration failed (offline, server error); retried next launch.
 */
export type PushState = 'checking' | 'unsupported' | 'default' | 'denied' | 'registered' | 'failed';
