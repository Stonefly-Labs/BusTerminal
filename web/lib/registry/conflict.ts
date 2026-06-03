/**
 * Spec 006 / T054. Conflict-response parsing + diff helpers.
 *
 * The backend produces the diff (server has both shapes; client only has
 * its submission), but the form layer needs local helpers to:
 *   - extract a `ConflictResponse` from a failed PUT,
 *   - render the diff in the conflict modal (T101),
 *   - drive the "Discard & refresh" reset path via `form.reset(currentEntity)`.
 *
 * These helpers are pure — they have no IO and no React dependencies.
 */

import type { ConflictResponse, RegistryEntity } from "./schemas";
import { conflictResponseSchema } from "./schemas";

/**
 * `httpJson` throws an Error with `.status` set; this helper checks whether
 * the thrown error carries a parseable 409 conflict body. Returns the
 * parsed shape, or `null` if the error wasn't a conflict.
 */
export async function parseConflictResponse(
  error: unknown,
  responseBody?: string,
): Promise<ConflictResponse | null> {
  if (!isHttpError(error)) return null;
  if (error.status !== 409) return null;
  if (!responseBody) return null;

  try {
    const json = JSON.parse(responseBody);
    return conflictResponseSchema.parse(json);
  } catch {
    return null;
  }
}

/**
 * Computes a per-field diff between two RegistryEntity shapes, suitable for
 * the conflict modal. Excludes server-managed fields (createdAtUtc,
 * updatedAtUtc, fullyQualifiedName) which match the backend's exclusion list
 * in ConcurrencyConflictMapper.
 */
export function diffEntities(
  current: RegistryEntity,
  submitted: RegistryEntity,
): Array<{ field: string; currentValue: unknown; submittedValue: unknown }> {
  const excluded = new Set([
    "createdAtUtc",
    "updatedAtUtc",
    "fullyQualifiedName",
  ]);

  // Cast to record so we can index by string. Each canonical field is
  // either a primitive or an array/object — JSON.stringify comparison is
  // good enough for the conflict modal's display purposes.
  const c = current as unknown as Record<string, unknown>;
  const s = submitted as unknown as Record<string, unknown>;
  const fields = new Set<string>([...Object.keys(c), ...Object.keys(s)]);

  const changes: Array<{ field: string; currentValue: unknown; submittedValue: unknown }> = [];
  for (const field of fields) {
    if (excluded.has(field)) continue;
    const cv = c[field];
    const sv = s[field];
    if (!shallowJsonEquals(cv, sv)) {
      changes.push({ field, currentValue: cv, submittedValue: sv });
    }
  }
  return changes;
}

function shallowJsonEquals(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (a == null || b == null) return false;
  return JSON.stringify(a) === JSON.stringify(b);
}

function isHttpError(error: unknown): error is Error & { status: number } {
  return (
    error instanceof Error &&
    "status" in error &&
    typeof (error as { status?: unknown }).status === "number"
  );
}
