/**
 * Spec 008 / T065. Wizard sessionStorage wrapper — round-trip, debounce,
 * explicit clear, beforeunload listener teardown.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  WIZARD_STORAGE_KEY,
  createWizardStorage,
} from "@/lib/namespaces/wizard-storage";

interface SampleValue {
  readonly armId: string;
  readonly displayName: string;
}

const SAMPLE: SampleValue = {
  armId: "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns",
  displayName: "Orders",
};

describe("wizardStorage", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    sessionStorage.clear();
  });

  it("round-trips save/load through sessionStorage with debounce", () => {
    const storage = createWizardStorage<SampleValue>({ debounceMs: 100 });

    storage.save(SAMPLE);
    expect(storage.load()).toBeNull(); // not yet persisted (still debounced)

    vi.advanceTimersByTime(100);
    expect(storage.load()).toEqual(SAMPLE);
    expect(sessionStorage.getItem(WIZARD_STORAGE_KEY)).toContain("Orders");
  });

  it("clear() removes the persisted item immediately", () => {
    const storage = createWizardStorage<SampleValue>({ debounceMs: 0 });
    storage.save(SAMPLE);
    vi.advanceTimersByTime(1);

    storage.clear();
    expect(storage.load()).toBeNull();
    expect(sessionStorage.getItem(WIZARD_STORAGE_KEY)).toBeNull();
  });

  it("debounces multiple saves into a single write", () => {
    const storage = createWizardStorage<SampleValue>({ debounceMs: 100 });

    storage.save({ ...SAMPLE, displayName: "A" });
    vi.advanceTimersByTime(50);
    storage.save({ ...SAMPLE, displayName: "B" });
    vi.advanceTimersByTime(50);
    storage.save({ ...SAMPLE, displayName: "C" });
    vi.advanceTimersByTime(100);

    const persisted = storage.load();
    expect(persisted?.displayName).toBe("C");
  });

  it("clearOnBeforeUnload registers and unregisters the listener", () => {
    const storage = createWizardStorage<SampleValue>({ debounceMs: 0 });
    storage.save(SAMPLE);
    vi.advanceTimersByTime(1);
    expect(storage.load()).toEqual(SAMPLE);

    const teardown = storage.clearOnBeforeUnload();
    window.dispatchEvent(new Event("beforeunload"));
    expect(storage.load()).toBeNull();

    teardown();
  });

  it("subscribers are notified on save and clear", () => {
    const storage = createWizardStorage<SampleValue>({ debounceMs: 0 });
    const subscriber = vi.fn();
    const unsubscribe = storage.subscribe(subscriber);

    storage.save(SAMPLE);
    vi.advanceTimersByTime(1);
    expect(subscriber).toHaveBeenLastCalledWith(SAMPLE);

    storage.clear();
    expect(subscriber).toHaveBeenLastCalledWith(null);

    unsubscribe();
  });
});
