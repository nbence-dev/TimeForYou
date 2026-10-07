/**
 * Keeps overlapping requests from clobbering each other. Each request calls `next()` when it
 * starts and gets back two checks:
 * - `isNewest()`: no newer request has started. Failures and the spinner follow this, so only the
 *   latest request reports a problem.
 * - `claimApply()`: no newer request has already applied its result. A success follows this, so a
 *   slow older response cannot overwrite a newer one, but still lands if the newer one failed.
 */
export function createLatestGuard() {
  let latest = 0;
  let applied = 0;
  return {
    next() {
      const id = ++latest;
      return {
        isNewest: () => id === latest,
        claimApply: () => {
          if (id <= applied) return false;
          applied = id;
          return true;
        },
      };
    },
  };
}
