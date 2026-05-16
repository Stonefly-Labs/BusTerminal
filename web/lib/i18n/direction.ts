/**
 * Locale-to-direction mapping (FR-022b / FR-022d).
 *
 * v1 content is English-only, but every primitive is RTL-safe by
 * construction (CSS logical properties only — enforced by
 * `pnpm audit:directions`). `directionForLocale` is consumed by
 * `app/layout.tsx` to set `<html dir>` and by the Storybook RTL toggle so
 * reviewers can verify every primitive renders without breakage under
 * `dir="rtl"` (SC-011).
 */

import type { SupportedLocale } from "./format";

export type Direction = "ltr" | "rtl";

/**
 * BCP-47 primary subtags whose languages render right-to-left. Sourced from
 * the CLDR right-to-left script list, filtered to languages with meaningful
 * browser locale-tag usage.
 */
const RTL_LANGUAGES = new Set([
  "ar", // Arabic
  "arc", // Aramaic
  "ckb", // Sorani Kurdish
  "dv", // Divehi
  "fa", // Persian
  "ha", // Hausa
  "he", // Hebrew
  "khw", // Khowar
  "ks", // Kashmiri
  "ps", // Pashto
  "sd", // Sindhi
  "ur", // Urdu
  "uz-AF", // Uzbek (Afghanistan, written in Arabic script)
  "yi", // Yiddish
]);

/**
 * Returns the writing direction for a locale tag. Falls back to `ltr` for
 * unknown locales.
 */
export function directionForLocale(locale: SupportedLocale): Direction {
  if (!locale) return "ltr";
  const normalized = locale.toLowerCase();
  if (RTL_LANGUAGES.has(normalized)) return "rtl";
  const primary = normalized.split("-")[0];
  if (primary && RTL_LANGUAGES.has(primary)) return "rtl";
  return "ltr";
}
