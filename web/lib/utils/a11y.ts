/**
 * Shared accessibility utilities (FR-034).
 *
 * Consumed by Dialog (T052), Sheet (T053), Command palette (T056), Drawer
 * patterns, and the destructive confirmation composite (T078). The FR-034
 * mandate ("any required accessibility utilities MUST be published as
 * shared foundation utilities") collapses these into one module so every
 * overlay primitive uses the same focus-management contract.
 */

"use client";

import { useCallback, useEffect, useRef, type RefObject } from "react";

// -----------------------------------------------------------------------------
// useFocusTrap — Tab / Shift+Tab cycling inside a region
// -----------------------------------------------------------------------------

const FOCUSABLE_SELECTOR = [
  "a[href]",
  "area[href]",
  "button:not([disabled])",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "iframe",
  "object",
  "embed",
  "[contenteditable]:not([contenteditable='false'])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function focusableWithin(container: HTMLElement): HTMLElement[] {
  const candidates = container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR);
  return Array.from(candidates).filter(
    (element) => !element.hasAttribute("disabled") && element.offsetParent !== null,
  );
}

/**
 * Trap Tab / Shift+Tab inside the region. The first focusable element is
 * focused on mount (unless `autoFocus` is false). Pair with `useRestoreFocus`
 * to return focus to the opener on close.
 */
export function useFocusTrap(
  containerRef: RefObject<HTMLElement | null>,
  options: { readonly active?: boolean; readonly autoFocus?: boolean } = {},
): void {
  const { active = true, autoFocus = true } = options;
  useEffect(() => {
    const container = containerRef.current;
    if (!active || !container) return;

    if (autoFocus) {
      const focusables = focusableWithin(container);
      const first = focusables[0] ?? container;
      first.focus({ preventScroll: true });
    }

    function handleKeydown(event: KeyboardEvent): void {
      if (event.key !== "Tab" || !container) return;
      const focusables = focusableWithin(container);
      if (focusables.length === 0) {
        event.preventDefault();
        container.focus({ preventScroll: true });
        return;
      }
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement as HTMLElement | null;
      if (!first || !last) return;
      if (event.shiftKey && (active === first || !container.contains(active))) {
        event.preventDefault();
        last.focus({ preventScroll: true });
      } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus({ preventScroll: true });
      }
    }

    document.addEventListener("keydown", handleKeydown);
    return () => document.removeEventListener("keydown", handleKeydown);
  }, [containerRef, active, autoFocus]);
}

// -----------------------------------------------------------------------------
// useRestoreFocus — return focus to the opener when an overlay closes
// -----------------------------------------------------------------------------

export function useRestoreFocus(triggerRef: RefObject<HTMLElement | null>): void {
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    previouslyFocusedRef.current =
      (document.activeElement as HTMLElement | null) ?? null;
    const triggerSnapshot = triggerRef;
    const fallbackSnapshot = previouslyFocusedRef;

    return () => {
      const target = triggerSnapshot.current ?? fallbackSnapshot.current;
      if (target && typeof target.focus === "function") {
        target.focus({ preventScroll: true });
      }
    };
  }, [triggerRef]);
}

// -----------------------------------------------------------------------------
// usePressEscape — close the active overlay layer
// -----------------------------------------------------------------------------

export function usePressEscape(
  handler: (event: KeyboardEvent) => void,
  options: { readonly active?: boolean } = {},
): void {
  const { active = true } = options;
  const handlerRef = useRef(handler);

  useEffect(() => {
    handlerRef.current = handler;
  }, [handler]);

  useEffect(() => {
    if (!active) return;
    function onKey(event: KeyboardEvent): void {
      if (event.key === "Escape" || event.key === "Esc") {
        handlerRef.current(event);
      }
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [active]);
}

// -----------------------------------------------------------------------------
// getAccessibleName — resolve the effective accessible name of an element
// -----------------------------------------------------------------------------

/**
 * Returns the best-effort accessible name for an element following the
 * common precedence: `aria-labelledby` → `aria-label` → `<label for>` →
 * inner text. Useful in tests and in primitives that announce focus
 * destination dynamically.
 */
export function getAccessibleName(element: HTMLElement | null): string {
  if (!element) return "";
  const labelledBy = element.getAttribute("aria-labelledby");
  if (labelledBy) {
    const labels = labelledBy
      .split(/\s+/)
      .map((id) => document.getElementById(id)?.textContent?.trim() ?? "")
      .filter(Boolean);
    if (labels.length > 0) return labels.join(" ");
  }
  const ariaLabel = element.getAttribute("aria-label");
  if (ariaLabel) return ariaLabel.trim();
  if (element.id) {
    const associatedLabel = document.querySelector(
      `label[for="${CSS.escape(element.id)}"]`,
    );
    if (associatedLabel?.textContent) return associatedLabel.textContent.trim();
  }
  return element.textContent?.trim() ?? "";
}

/**
 * Convenience callback for usePressEscape consumers that want a stable
 * inline handler.
 */
export function useStableCallback<Args extends readonly unknown[]>(
  callback: (...args: Args) => void,
): (...args: Args) => void {
  const ref = useRef(callback);
  useEffect(() => {
    ref.current = callback;
  }, [callback]);
  return useCallback((...args: Args) => ref.current(...args), []);
}
