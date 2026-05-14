/**
 * Theme Provider Contract
 *
 * Spec references:
 *   - FR-005 (dark + light as first-class peers)
 *   - FR-006 (system preference honored on first load; user overrideable; persisted; no flash)
 *   - SC-004 (no visible flash of incorrect theme)
 *
 * Implementation: next-themes with the `class` strategy, an inline anti-FOUC
 * script in <head>, and localStorage persistence under the key `bt:theme`.
 */

export type ThemeId = 'light' | 'dark';

/**
 * The user's stored preference. `'system'` is the resolver instruction —
 * not a target binding — that defers to the browser's prefers-color-scheme.
 */
export type ThemePreference = 'light' | 'dark' | 'system';

export interface ThemeProviderState {
  /** The currently rendered theme. Resolved from `preference`. */
  readonly resolved: ThemeId;
  /** The user's stored preference. */
  readonly preference: ThemePreference;
  /** Whether the resolver has read the persisted value. False during the inline-script gap. */
  readonly isHydrated: boolean;
}

export interface ThemeProviderActions {
  /** Sets the user's explicit preference; persists to localStorage. */
  setPreference(next: ThemePreference): void;
  /** Toggles between light and dark. If the current preference is `'system'`, resolves first then toggles. */
  toggle(): void;
  /** Clears the persisted preference, returning to `'system'`. */
  reset(): void;
}

/**
 * Hook consumed by primitives that need to react to the active theme
 * (e.g., chart wrappers that swap data colors).
 */
export declare function useTheme(): ThemeProviderState & ThemeProviderActions;

/**
 * The `localStorage` key under which the theme preference is persisted.
 * Stable across foundation versions to preserve user choice.
 */
export const THEME_STORAGE_KEY = 'bt:theme';
