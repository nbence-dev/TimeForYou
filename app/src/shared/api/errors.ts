/** The API answered with a non-2xx status. `message` is the API's `Error.message`. */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

/** HTTP 401. The stored token has already been cleared; send the student to onboarding. */
export class UnauthorizedError extends ApiError {
  constructor(message: string) {
    super(401, message);
    this.name = 'UnauthorizedError';
  }
}

/** The request never got an HTTP response (offline, DNS, CORS, tunnel down). */
export class NetworkError extends Error {
  constructor(cause?: unknown) {
    super('Could not reach the server');
    this.name = 'NetworkError';
    this.cause = cause;
  }
}
