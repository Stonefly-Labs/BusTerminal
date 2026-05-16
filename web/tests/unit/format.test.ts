import { describe, it, expect } from "vitest";

import {
  formatBytes,
  formatDate,
  formatDuration,
  formatNumber,
  formatRelativeTime,
  formatTime,
} from "@/lib/i18n/format";

/**
 * Locale-aware formatter unit tests (T154 / FR-022c).
 *
 * Exercises each formatter against three non-default locales chosen for
 * meaningful differences:
 *
 *   - `de-DE` — day-month-year ordering, `.` thousands separator, `,`
 *     decimal separator.
 *   - `ja-JP` — year-month-day ordering, `年/月/日` separators (or `/`
 *     depending on options).
 *   - `ar-EG` — RTL, Arabic-Indic digits in the default numbering
 *     system. Confirms the formatters emit locale-correct numerals
 *     even though v1 content is English-only (the foundation must be
 *     RTL-safe by construction).
 *
 * The assertions are structural rather than full string equality where
 * the underlying ICU build can vary across Node versions — we check
 * for the distinguishing characters / digit ranges rather than fragile
 * byte-for-byte exact matches.
 *
 * The Vitest environment is `jsdom` and Node 20.x ships a recent ICU
 * with full data for these locales, so the underlying `Intl.*`
 * implementations behave like a browser.
 */

const FIXED_DATE = new Date("2026-05-14T09:00:00.000Z");

describe("formatDate", () => {
  it("formats day-month-year ordering for de-DE", () => {
    const result = formatDate(FIXED_DATE, { locale: "de-DE" });
    // de-DE short month: "Mai" — the day appears before the month.
    expect(result).toMatch(/14/);
    expect(result).toMatch(/Mai|05/);
    expect(result).toMatch(/2026/);
    const dayIndex = result.indexOf("14");
    const yearIndex = result.indexOf("2026");
    expect(dayIndex).toBeGreaterThanOrEqual(0);
    expect(yearIndex).toBeGreaterThan(dayIndex);
  });

  it("formats year-first ordering for ja-JP", () => {
    const result = formatDate(FIXED_DATE, { locale: "ja-JP" });
    expect(result).toMatch(/2026/);
    expect(result).toMatch(/14/);
    const yearIndex = result.indexOf("2026");
    const dayIndex = result.indexOf("14");
    expect(yearIndex).toBeGreaterThanOrEqual(0);
    expect(dayIndex).toBeGreaterThan(yearIndex);
  });

  it("uses the Arabic-Indic numbering system for ar-EG", () => {
    const result = formatDate(FIXED_DATE, { locale: "ar-EG" });
    // ar-EG defaults to the `arab` numbering system (Arabic-Indic
    // digits ٠-٩, U+0660…U+0669). We assert at least one Arabic-Indic
    // digit appears in the output.
    expect(/[٠-٩]/.test(result)).toBe(true);
  });
});

describe("formatTime", () => {
  it("returns a non-empty string for each locale", () => {
    for (const locale of ["de-DE", "ja-JP", "ar-EG"]) {
      const result = formatTime(FIXED_DATE, { locale });
      expect(result.length).toBeGreaterThan(0);
    }
  });
});

describe("formatRelativeTime", () => {
  it("renders the de-DE word for 'in 5 minutes' or 'vor 5 Minuten'", () => {
    const future = formatRelativeTime(5, "minute", { locale: "de-DE" });
    expect(future.toLowerCase()).toMatch(/minute|min\./);

    const past = formatRelativeTime(-5, "minute", { locale: "de-DE" });
    expect(past.toLowerCase()).toMatch(/minute|min\./);
  });

  it("renders Japanese unit name for ja-JP", () => {
    const result = formatRelativeTime(3, "hour", { locale: "ja-JP" });
    // ja-JP uses 時間 (hour). We confirm at least one Japanese
    // character appears.
    expect(/[　-鿿]/.test(result)).toBe(true);
  });

  it("uses Arabic-Indic digits for ar-EG", () => {
    // Arabic has dual / plural grammatical forms — `2 day` returns the
    // dual form "يومين" (no digits). Use 5 to force the plural form
    // with a numeric "٥".
    const result = formatRelativeTime(5, "day", { locale: "ar-EG" });
    expect(/[٠-٩]/.test(result)).toBe(true);
  });
});

describe("formatDuration", () => {
  it("formats minutes for de-DE", () => {
    const result = formatDuration(15 * 60 * 1000, { locale: "de-DE" });
    expect(result.toLowerCase()).toMatch(/min/);
    expect(result).toMatch(/15/);
  });

  it("formats hours + minutes split for ja-JP", () => {
    const result = formatDuration(2 * 60 * 60 * 1000 + 14 * 60 * 1000, {
      locale: "ja-JP",
    });
    // Output contains "2 時間 14 分" or short variants. We assert both
    // the hour and minute numeric components are present.
    expect(result).toMatch(/2/);
    expect(result).toMatch(/14/);
  });

  it("uses Arabic-Indic digits for ar-EG", () => {
    const result = formatDuration(30 * 1000, { locale: "ar-EG" });
    expect(/[٠-٩]/.test(result)).toBe(true);
  });
});

describe("formatNumber", () => {
  it("uses `.` thousands and `,` decimal for de-DE", () => {
    const result = formatNumber(1234567.89, { locale: "de-DE" });
    expect(result).toContain(".");
    expect(result).toContain(",");
    // The decimal portion follows a comma.
    expect(result).toMatch(/,89/);
  });

  it("uses `,` thousands and `.` decimal for ja-JP", () => {
    const result = formatNumber(1234567.89, { locale: "ja-JP" });
    expect(result).toContain(",");
    expect(result).toContain(".");
    expect(result).toMatch(/\.89/);
  });

  it("uses Arabic-Indic digits for ar-EG", () => {
    const result = formatNumber(12345, { locale: "ar-EG" });
    expect(/[٠-٩]/.test(result)).toBe(true);
  });
});

describe("formatBytes", () => {
  it("emits unit suffix and locale-correct numeric component for de-DE", () => {
    const result = formatBytes(2 * 1024 * 1024, { locale: "de-DE" });
    expect(result).toMatch(/MiB/);
    // de-DE renders 2 without a separator; assert the digit itself.
    expect(result).toMatch(/2/);
  });

  it("emits unit suffix for ja-JP", () => {
    const result = formatBytes(512, { locale: "ja-JP" });
    expect(result).toMatch(/B$/);
    expect(result).toMatch(/512/);
  });

  it("uses Arabic-Indic digits for ar-EG", () => {
    const result = formatBytes(1024, { locale: "ar-EG" });
    expect(/[٠-٩]/.test(result)).toBe(true);
    expect(result).toMatch(/KiB/);
  });
});
