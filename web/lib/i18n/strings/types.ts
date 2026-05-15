/**
 * Shape declarations shared by the i18n string registry.
 *
 * The registry itself (`en.ts`) is the single source of truth for the
 * concrete key set; these types describe the per-entry shape.
 */

export interface StringEntry {
  /** Dotted, hierarchical identifier — e.g., `table.toolbar.search.placeholder`. */
  readonly key: string;
  /** The v1 English copy. May contain `{varName}` interpolation slots. */
  readonly englishValue: string;
  /** Translator-facing context (intent, surface, edge cases). */
  readonly description: string;
  /**
   * Declared interpolation slots and their semantic type. The slot type
   * informs translators and locale formatters; runtime substitution is
   * stringly-typed through `t(key, vars)`.
   */
  readonly interpolations: Readonly<Record<string, "string" | "number" | "date">>;
}
