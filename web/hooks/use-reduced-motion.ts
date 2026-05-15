"use client";

import * as React from "react";

/**
 * `useReducedMotion` (T108 / FR-025 / SC-008).
 *
 * Reads the user's `prefers-reduced-motion: reduce` operating-system setting
 * and re-renders the consumer when it changes. Used to gate non-essential
 * motion that CSS alone cannot suppress — JavaScript-driven animation
 * libraries (Recharts enter/update tweens, Framer Motion `animate` props,
 * Sonner toast slide-ins) need an imperative opt-out.
 *
 * Returns `false` during SSR and on first paint so server output matches
 * the not-yet-subscribed client output (avoids hydration mismatch). Once
 * subscribed, returns the live media-query value.
 *
 * Implementation: `useSyncExternalStore` subscribes to the media query
 * and reads its `matches` value snapshot-by-snapshot, which is the
 * recommended React 18+ pattern for media-query subscription. The
 * server snapshot is `false` so SSR renders with motion enabled — this
 * matches the default visual state and is a deliberate trade-off: it
 * means the first paint after hydration can begin a transition, but
 * the global `prefers-reduced-motion` rule in `app/globals.css`
 * collapses CSS-driven animations regardless. The hook exists so JS
 * animation can opt OUT once hydrated.
 */
export function useReducedMotion(): boolean {
  const subscribe = React.useCallback((onChange: () => void): (() => void) => {
    if (typeof window === "undefined") return () => {};
    const mql = window.matchMedia("(prefers-reduced-motion: reduce)");
    // Safari < 14 used the deprecated `addListener` API. The supported
    // browser matrix (FR-035a) guarantees `addEventListener` is present.
    mql.addEventListener("change", onChange);
    return () => mql.removeEventListener("change", onChange);
  }, []);

  const getClientSnapshot = React.useCallback((): boolean => {
    if (typeof window === "undefined") return false;
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  }, []);

  const getServerSnapshot = React.useCallback((): boolean => false, []);

  return React.useSyncExternalStore(subscribe, getClientSnapshot, getServerSnapshot);
}
