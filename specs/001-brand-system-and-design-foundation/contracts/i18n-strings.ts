/**
 * i18n String Surface + Locale Formatter Contract
 *
 * Spec references:
 *   - FR-022a (centralized string surface; no hardcoded strings in primitives/composites)
 *   - FR-022b (logical properties; no physical left/right)
 *   - FR-022c (locale-aware formatters for date/time/relative-time/duration/number/bytes)
 *   - FR-022d (dir="rtl" support without visual breakage)
 *   - SC-012 (audit finds zero hardcoded strings and zero physical directions)
 *
 * Implementation: a hand-rolled, type-safe key/value module in
 * `web/lib/i18n/strings/en.ts`, accessed through `t(key)`. No runtime
 * translation library is adopted in v1 — a future translation spec swaps
 * the implementation behind this contract without changing call sites.
 */

// -----------------------------------------------------------------------------
// String surface
// -----------------------------------------------------------------------------

/**
 * The set of registered i18n keys for the foundation. The actual key list is
 * exported from `web/lib/i18n/strings/en.ts`; this declaration captures the
 * SHAPE of the surface for documentation and audit purposes.
 *
 * Keys follow the dotted convention: `<surface>.<element>.<role>`.
 * Examples:
 *   - 'table.toolbar.search.placeholder'
 *   - 'dialog.destructive.confirmLabel'
 *   - 'feedback.empty.defaultTitle'
 *   - 'domain.deadLetter.label'
 */
export type StringKey = string;

export interface StringEntry {
  readonly key: StringKey;
  readonly englishValue: string;
  readonly description: string;
  readonly interpolations: Readonly<Record<string, 'string' | 'number' | 'date'>>;
}

/**
 * Returns the English value for a key. The `vars` argument is required when
 * the entry declares any interpolations; type-checking enforces presence and
 * named keys.
 */
export declare function t<K extends StringKey>(
  key: K,
  vars?: Record<string, string | number | Date>,
): string;

/**
 * Audit support — the full registry of declared keys is enumerated so the
 * `pnpm audit:strings` gate can verify that primitives and composites never
 * embed raw user-facing strings.
 */
export declare const ALL_STRING_KEYS: readonly StringKey[];

// -----------------------------------------------------------------------------
// Locale-aware formatters (FR-022c)
// -----------------------------------------------------------------------------

export type SupportedLocale = string; // BCP 47 locale tag; defaults to navigator.language

export interface FormatterOptions {
  /** Override the active locale. Defaults to the browser locale. */
  readonly locale?: SupportedLocale;
}

export declare function formatDate(
  value: Date | number,
  options?: FormatterOptions & Intl.DateTimeFormatOptions,
): string;

export declare function formatTime(
  value: Date | number,
  options?: FormatterOptions & Intl.DateTimeFormatOptions,
): string;

export declare function formatRelativeTime(
  value: number,
  unit: Intl.RelativeTimeFormatUnit,
  options?: FormatterOptions & Intl.RelativeTimeFormatOptions,
): string;

export declare function formatDuration(
  milliseconds: number,
  options?: FormatterOptions & { readonly granularity?: 'seconds' | 'minutes' | 'auto' },
): string;

export declare function formatNumber(
  value: number,
  options?: FormatterOptions & Intl.NumberFormatOptions,
): string;

/**
 * Formats a byte count using the closest power-of-two unit (B / KiB / MiB / GiB / TiB)
 * formatted with `Intl.NumberFormat`. Locale defaults to the browser locale.
 */
export declare function formatBytes(
  bytes: number,
  options?: FormatterOptions & { readonly maximumFractionDigits?: number },
): string;

// -----------------------------------------------------------------------------
// Direction helpers (FR-022b / FR-022d)
// -----------------------------------------------------------------------------

export type Direction = 'ltr' | 'rtl';

/**
 * Returns the document direction for a given locale. Used by `app/layout.tsx`
 * to set the `dir` attribute on `<html>` and by the Storybook RTL toggle.
 */
export declare function directionForLocale(locale: SupportedLocale): Direction;
