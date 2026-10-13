// Pure rules for the Changes archive (#138): which changes are new and which the student has
// archived. No React, no I/O.
//
// "Mark all as read" adds the ids of the changes on screen to a set kept on the device. A change
// the worker logs afterwards has an id that is not in the set, so it shows as new. Ids are never
// dropped, for the same reason as the seen ids in `change-markers.ts`. This is separate from the
// unseen dots: archiving never touches them.

/** The changes split into those not archived yet (new) and those archived, each in the order given. */
export function splitByArchive<T extends { id: string }>(
  changes: readonly T[],
  archived: ReadonlySet<string>,
): { fresh: T[]; archived: T[] } {
  return {
    fresh: changes.filter((change) => !archived.has(change.id)),
    archived: changes.filter((change) => archived.has(change.id)),
  };
}

/** `archived` plus every change given: the student pressed "Mark all as read". */
export function withArchived(archived: ReadonlySet<string>, changes: readonly { id: string }[]): Set<string> {
  const next = new Set(archived);
  for (const change of changes) next.add(change.id);
  return next;
}
