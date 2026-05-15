/**
 * Locale-aware formatters (FR-022c).
 *
 * All formatters wrap native `Intl.*` — no polyfills are required because
 * the supported browser matrix (FR-035a) guarantees full coverage. The
 * default locale is the browser's `navigator.language` (resolved at call
 * time, so users can change OS / browser language without a rebuild).
 *
 * These functions are pure and side-effect-free.
 *
 * Spec references:
 *   - `specs/001-brand-system-and-design-foundation/contracts/i18n-strings.ts`
 *   - Research R5 — locale-aware formatting
 */

export type SupportedLocale = string;

export interface FormatterOptions {
  readonly locale?: SupportedLocale;
}

function activeLocale(override?: SupportedLocale): string {
  if (override) return override;
  if (typeof navigator !== "undefined" && navigator.language) return navigator.language;
  return "en-US";
}

// -----------------------------------------------------------------------------
// Date / time
// -----------------------------------------------------------------------------

export function formatDate(
  value: Date | number,
  options?: FormatterOptions & Intl.DateTimeFormatOptions,
): string {
  const { locale, ...rest } = options ?? {};
  const formatter = new Intl.DateTimeFormat(activeLocale(locale), {
    year: "numeric",
    month: "short",
    day: "numeric",
    ...rest,
  });
  return formatter.format(value);
}

export function formatTime(
  value: Date | number,
  options?: FormatterOptions & Intl.DateTimeFormatOptions,
): string {
  const { locale, ...rest } = options ?? {};
  const formatter = new Intl.DateTimeFormat(activeLocale(locale), {
    hour: "numeric",
    minute: "2-digit",
    ...rest,
  });
  return formatter.format(value);
}

export function formatRelativeTime(
  value: number,
  unit: Intl.RelativeTimeFormatUnit,
  options?: FormatterOptions & Intl.RelativeTimeFormatOptions,
): string {
  const { locale, ...rest } = options ?? {};
  const formatter = new Intl.RelativeTimeFormat(activeLocale(locale), {
    numeric: "auto",
    ...rest,
  });
  return formatter.format(value, unit);
}

// -----------------------------------------------------------------------------
// Duration
// -----------------------------------------------------------------------------

type DurationGranularity = "seconds" | "minutes" | "auto";

const SECOND_MS = 1000;
const MINUTE_MS = 60 * SECOND_MS;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

/**
 * Format a millisecond duration as a human-readable, locale-aware string
 * (e.g., "2 hr 14 min", "45 sec"). Uses `Intl.NumberFormat` for the numeric
 * parts and emits unit suffixes via `Intl.NumberFormat`'s `unit` style where
 * supported (every browser in the FR-035a matrix).
 */
export function formatDuration(
  milliseconds: number,
  options?: FormatterOptions & { readonly granularity?: DurationGranularity },
): string {
  const { locale, granularity = "auto" } = options ?? {};
  const loc = activeLocale(locale);
  const abs = Math.abs(milliseconds);

  if (granularity === "seconds" || (granularity === "auto" && abs < MINUTE_MS)) {
    return new Intl.NumberFormat(loc, {
      style: "unit",
      unit: "second",
      unitDisplay: "short",
      maximumFractionDigits: 0,
    }).format(Math.round(milliseconds / SECOND_MS));
  }

  if (granularity === "minutes" || (granularity === "auto" && abs < HOUR_MS)) {
    return new Intl.NumberFormat(loc, {
      style: "unit",
      unit: "minute",
      unitDisplay: "short",
      maximumFractionDigits: 0,
    }).format(Math.round(milliseconds / MINUTE_MS));
  }

  if (abs < DAY_MS) {
    const hours = Math.floor(milliseconds / HOUR_MS);
    const remainingMinutes = Math.round((milliseconds - hours * HOUR_MS) / MINUTE_MS);
    const hoursPart = new Intl.NumberFormat(loc, {
      style: "unit",
      unit: "hour",
      unitDisplay: "short",
      maximumFractionDigits: 0,
    }).format(hours);
    if (remainingMinutes === 0) return hoursPart;
    const minutesPart = new Intl.NumberFormat(loc, {
      style: "unit",
      unit: "minute",
      unitDisplay: "short",
      maximumFractionDigits: 0,
    }).format(remainingMinutes);
    return `${hoursPart} ${minutesPart}`;
  }

  return new Intl.NumberFormat(loc, {
    style: "unit",
    unit: "day",
    unitDisplay: "short",
    maximumFractionDigits: 1,
  }).format(milliseconds / DAY_MS);
}

// -----------------------------------------------------------------------------
// Numbers
// -----------------------------------------------------------------------------

export function formatNumber(
  value: number,
  options?: FormatterOptions & Intl.NumberFormatOptions,
): string {
  const { locale, ...rest } = options ?? {};
  const formatter = new Intl.NumberFormat(activeLocale(locale), rest);
  return formatter.format(value);
}

// -----------------------------------------------------------------------------
// Bytes
// -----------------------------------------------------------------------------

const BYTE_UNITS = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"] as const;

/**
 * Format a byte count using the closest power-of-two unit (B / KiB / MiB /
 * GiB / TiB / PiB) and `Intl.NumberFormat` for the numeric component.
 */
export function formatBytes(
  bytes: number,
  options?: FormatterOptions & { readonly maximumFractionDigits?: number },
): string {
  const { locale, maximumFractionDigits = 1 } = options ?? {};
  const loc = activeLocale(locale);
  const abs = Math.abs(bytes);
  const exponent = abs === 0 ? 0 : Math.min(
    Math.floor(Math.log2(abs) / 10),
    BYTE_UNITS.length - 1,
  );
  const unit = BYTE_UNITS[exponent] ?? BYTE_UNITS[0];
  const scaled = bytes / 2 ** (exponent * 10);
  const numeric = new Intl.NumberFormat(loc, {
    maximumFractionDigits: exponent === 0 ? 0 : maximumFractionDigits,
  }).format(scaled);
  return `${numeric} ${unit}`;
}
