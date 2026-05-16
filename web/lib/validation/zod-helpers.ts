import { z } from "zod";

/**
 * Reusable Zod helpers shared across form composites (FR-017 / T080).
 * Keep helpers behavior-driven, not domain-specific — domain schemas live with
 * their feature.
 */

export function requiredString(opts?: { readonly min?: number; readonly max?: number; readonly message?: string }) {
  const { min = 1, max, message = "Required" } = opts ?? {};
  let schema = z.string().min(min, message);
  if (max !== undefined) schema = schema.max(max);
  return schema;
}

export function optionalBoundedString(opts?: { readonly max?: number }) {
  const { max = 256 } = opts ?? {};
  return z.string().max(max).optional().or(z.literal(""));
}

export function enumFromTuple<T extends readonly [string, ...string[]]>(values: T) {
  return z.enum(values);
}

export function azurePartialId(opts?: { readonly subscriptionScope?: boolean }) {
  const { subscriptionScope = false } = opts ?? {};
  const pattern = subscriptionScope
    ? /^\/subscriptions\/[0-9a-f-]{36}\//i
    : /^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$/i;
  return z.string().regex(pattern, "Invalid Azure identifier");
}

export function bytesPositive() {
  return z.number().int().nonnegative();
}
