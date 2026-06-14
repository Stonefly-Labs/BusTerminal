/**
 * Spec 008 / T052 + research §9. sessionStorage wrapper for the
 * 5-step onboarding wizard. Debounced save (300ms) keeps every step's
 * input through back-navigation; explicit `clear()` runs on
 *   (a) successful registration,
 *   (b) explicit cancel,
 *   (c) window beforeunload.
 *
 * Bounded to the browser tab — closing the tab clears it. Per FR-002
 * the wizard's transient state NEVER travels to the backend until
 * step-5 register.
 */

export const WIZARD_STORAGE_KEY = "bt:namespaces:wizard:v1";
export const DEFAULT_SAVE_DEBOUNCE_MS = 300;

type Subscriber<TValue> = (value: TValue | null) => void;

export interface WizardStorage<TValue> {
  readonly load: () => TValue | null;
  readonly save: (value: TValue) => void;
  readonly clear: () => void;
  readonly clearOnBeforeUnload: () => () => void;
  readonly subscribe: (subscriber: Subscriber<TValue>) => () => void;
}

function getStorage(): Storage | null {
  if (typeof window === "undefined") return null;
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

export interface CreateWizardStorageOptions {
  readonly storageKey?: string;
  readonly debounceMs?: number;
}

export function createWizardStorage<TValue>(
  options: CreateWizardStorageOptions = {},
): WizardStorage<TValue> {
  const storageKey = options.storageKey ?? WIZARD_STORAGE_KEY;
  const debounceMs = options.debounceMs ?? DEFAULT_SAVE_DEBOUNCE_MS;
  const subscribers = new Set<Subscriber<TValue>>();
  let saveTimer: ReturnType<typeof setTimeout> | null = null;
  let pendingValue: TValue | null = null;

  function notify(value: TValue | null): void {
    for (const subscriber of subscribers) subscriber(value);
  }

  function load(): TValue | null {
    const storage = getStorage();
    if (!storage) return null;
    const raw = storage.getItem(storageKey);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as TValue;
    } catch {
      storage.removeItem(storageKey);
      return null;
    }
  }

  function save(value: TValue): void {
    pendingValue = value;
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      saveTimer = null;
      const storage = getStorage();
      if (!storage || pendingValue === null) return;
      try {
        storage.setItem(storageKey, JSON.stringify(pendingValue));
        notify(pendingValue);
      } catch {
        // sessionStorage may be full or disabled — fail open per FR-002:
        // wizard state is best-effort, not a hard contract.
      }
    }, debounceMs);
  }

  function clear(): void {
    if (saveTimer) {
      clearTimeout(saveTimer);
      saveTimer = null;
    }
    pendingValue = null;
    const storage = getStorage();
    if (storage) storage.removeItem(storageKey);
    notify(null);
  }

  function clearOnBeforeUnload(): () => void {
    if (typeof window === "undefined") return () => {};
    const handler = () => clear();
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }

  function subscribe(subscriber: Subscriber<TValue>): () => void {
    subscribers.add(subscriber);
    return () => subscribers.delete(subscriber);
  }

  return { load, save, clear, clearOnBeforeUnload, subscribe };
}

// Singleton instance keyed to the wizard's canonical storage slot.
// Multi-tab scenarios are out of scope (the wizard is single-instance per
// tab per spec FR-002).
export const wizardStorage = createWizardStorage<unknown>();
