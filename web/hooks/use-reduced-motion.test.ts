import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { renderHook, act } from "@testing-library/react";

import { useReducedMotion } from "./use-reduced-motion";

/**
 * Tests for `useReducedMotion` (T108 / FR-025 / SC-008).
 *
 * jsdom installs no real `matchMedia` implementation; the global vitest
 * setup stubs it to always return `matches: false`. These tests override
 * the stub per-case to exercise both states + change subscription.
 */

type Listener = (event: { matches: boolean }) => void;

function installMatchMediaStub(initialMatches: boolean) {
  const listeners = new Set<Listener>();
  const mql = {
    matches: initialMatches,
    media: "(prefers-reduced-motion: reduce)",
    onchange: null,
    addEventListener: (_: string, listener: Listener) => listeners.add(listener),
    removeEventListener: (_: string, listener: Listener) => listeners.delete(listener),
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  };
  const stub = vi.fn(() => mql);
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    configurable: true,
    value: stub,
  });
  return {
    fire(next: boolean) {
      mql.matches = next;
      listeners.forEach((listener) => listener({ matches: next }));
    },
  };
}

describe("useReducedMotion", () => {
  let originalMatchMedia: typeof window.matchMedia;

  beforeEach(() => {
    originalMatchMedia = window.matchMedia;
  });

  afterEach(() => {
    Object.defineProperty(window, "matchMedia", {
      writable: true,
      configurable: true,
      value: originalMatchMedia,
    });
  });

  it("returns false when the user has not requested reduced motion", () => {
    installMatchMediaStub(false);
    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(false);
  });

  it("returns true when the user has requested reduced motion", () => {
    installMatchMediaStub(true);
    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(true);
  });

  it("re-renders the consumer when the media-query value changes", () => {
    const stub = installMatchMediaStub(false);
    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(false);

    act(() => {
      stub.fire(true);
    });
    expect(result.current).toBe(true);

    act(() => {
      stub.fire(false);
    });
    expect(result.current).toBe(false);
  });
});
