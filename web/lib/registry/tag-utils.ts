/**
 * Spec 006 / T055 / research §9. Tag helpers for case-insensitive key match
 * + multi-value-per-key semantics.
 *
 * The backend persistence layer canonicalizes display-casing on write
 * (RegistryEntityValidationRules.NormalizeTagsForWrite); these helpers are
 * the equivalent helpers the form layer uses when comparing client-side
 * tag state to a previously-loaded server-side state.
 */

import type { RegistryTag } from "./schemas";

/**
 * Case-insensitive key match. Returns true when `a.key.toLowerCase() ===
 * b.key.toLowerCase()` and `a.value === b.value` (value match is
 * case-sensitive per the registry contract).
 */
export function tagEquals(a: RegistryTag, b: RegistryTag): boolean {
  return tagKeyLower(a.key) === tagKeyLower(b.key) && a.value === b.value;
}

/** Lowercase-Invariant projection used for key matching. */
export function tagKeyLower(key: string): string {
  return key.toLocaleLowerCase("en-US");
}

/**
 * Returns the distinct lowercase keys for the input tag list (de-duped).
 * Mirrors the persistence layer's `tagKeysLower` projection so the form
 * layer can compute the same shape locally for optimistic UI.
 */
export function distinctTagKeysLower(tags: readonly RegistryTag[]): string[] {
  const set = new Set<string>();
  for (const t of tags) {
    set.add(tagKeyLower(t.key));
  }
  return Array.from(set).sort();
}

/**
 * First-write-wins display normalization (research §9). When the submitted
 * tag's key matches an existing persisted key case-insensitively, the
 * persisted casing wins.
 */
export function normalizeTagsForWrite(
  submitted: readonly RegistryTag[],
  persisted: readonly RegistryTag[] | undefined,
): RegistryTag[] {
  if (submitted.length === 0) return [];

  const persistedKeyByLower = new Map<string, string>();
  if (persisted) {
    for (const t of persisted) {
      const lower = tagKeyLower(t.key);
      if (!persistedKeyByLower.has(lower)) {
        persistedKeyByLower.set(lower, t.key);
      }
    }
  }

  const canonicalCaseByLower = new Map<string, string>();
  return submitted.map((t) => {
    const lower = tagKeyLower(t.key);
    const persistedCase = persistedKeyByLower.get(lower);
    if (persistedCase) {
      return { key: persistedCase, value: t.value };
    }
    const alreadyCanonical = canonicalCaseByLower.get(lower);
    if (alreadyCanonical) {
      return { key: alreadyCanonical, value: t.value };
    }
    canonicalCaseByLower.set(lower, t.key);
    return { key: t.key, value: t.value };
  });
}

/**
 * Group tags by lowercase key for UI rendering (e.g. "Owner: Alice, Bob").
 * Preserves source order within each group.
 */
export function groupTagsByKeyLower(tags: readonly RegistryTag[]): Map<string, RegistryTag[]> {
  const groups = new Map<string, RegistryTag[]>();
  for (const t of tags) {
    const lower = tagKeyLower(t.key);
    const existing = groups.get(lower);
    if (existing) {
      existing.push(t);
    } else {
      groups.set(lower, [t]);
    }
  }
  return groups;
}
