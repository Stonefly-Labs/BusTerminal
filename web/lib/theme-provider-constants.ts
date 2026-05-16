/**
 * Theme-provider constants extracted to a server-safe module so `app/layout.tsx`
 * (RSC) and `app/providers.tsx` (client) can share them without forcing the
 * client boundary down into the layout.
 *
 * Mirrors the stable identifier in
 * `specs/001-brand-system-and-design-foundation/contracts/theme-provider.ts`.
 */

export const THEME_STORAGE_KEY = "bt:theme" as const;
