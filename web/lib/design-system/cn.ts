import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Conditional Tailwind class composition (FR-014).
 *
 * `cn` accepts any `clsx`-compatible value, joins truthy entries, and runs
 * `tailwind-merge` to resolve conflicting Tailwind utilities so the rightmost
 * declaration wins. The combined helper is the only sanctioned way to compose
 * class names across primitives and composites.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
