"use client";

/**
 * Spec 006 / T112. Search input with debounced typeahead. Used on the
 * /registry/search route; the global app-shell version composes this
 * same component inside a CommandDialog (T115).
 */

import { useEffect, useRef, useState } from "react";
import { Search } from "lucide-react";

import { Input } from "@/components/ui/input";
import { cn } from "@/lib/design-system/cn";

interface RegistrySearchInputProps {
  readonly value: string;
  readonly onChange: (next: string) => void;
  readonly autoFocus?: boolean | undefined;
  readonly placeholder?: string | undefined;
  readonly debounceMs?: number | undefined;
  readonly className?: string | undefined;
  readonly id?: string | undefined;
}

export function RegistrySearchInput({
  value,
  onChange,
  autoFocus,
  placeholder = "Search namespaces, queues, topics, subscriptions, rules…",
  debounceMs = 250,
  className,
  id,
}: RegistrySearchInputProps) {
  const [local, setLocal] = useState(value);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Keep the local mirror in sync when the parent resets (URL nav, etc.).
  useEffect(() => {
    setLocal(value);
  }, [value]);

  useEffect(() => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      if (local !== value) onChange(local);
    }, debounceMs);
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [local, debounceMs]);

  return (
    <div className={cn("relative flex items-center", className)} data-testid="registry-search-input">
      <Search
        aria-hidden="true"
        className="pointer-events-none absolute start-3 size-4 text-foreground-muted"
      />
      <Input
        id={id}
        type="search"
        autoFocus={autoFocus}
        placeholder={placeholder}
        value={local}
        onChange={(e) => setLocal(e.target.value)}
        aria-label="Search registry"
        className="ps-9"
      />
    </div>
  );
}
