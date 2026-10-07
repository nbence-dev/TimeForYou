/**
 * Lets only the newest of several overlapping requests apply its result. Each request calls
 * `next()` when it starts and checks the function it gets back before touching state: it returns
 * `true` only while no newer request has started. Without it, a slow older response that lands
 * last would overwrite a newer one.
 */
export function createLatestGuard() {
  let latest = 0;
  return {
    next(): () => boolean {
      const id = ++latest;
      return () => id === latest;
    },
  };
}
