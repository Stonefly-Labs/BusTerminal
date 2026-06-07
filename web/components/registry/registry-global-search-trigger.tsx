"use client";

/**
 * Spec 006 / T115. Global search affordance for the app shell. Renders a
 * compact button that opens a CommandDialog with a single input — on submit,
 * navigates to `/registry/search?q=…` (the URL is shareable + back/forward
 * friendly).
 *
 * Two surfaces compose this:
 *   - The platform NavigationHeader (visible across every page).
 *   - The registry layout header (already env-scoped).
 */

import { useState, useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import type { Route } from "next";
import { Search } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogTitle, DialogDescription } from "@/components/ui/dialog";

import { RegistrySearchInput } from "./registry-search-input";

interface RegistryGlobalSearchTriggerProps {
  readonly className?: string | undefined;
  readonly placement?: "header" | "registry" | undefined;
  readonly extraEnv?: string | undefined;
  readonly trigger?: ReactNode | undefined;
}

export function RegistryGlobalSearchTrigger({
  className,
  placement = "header",
  extraEnv,
  trigger,
}: RegistryGlobalSearchTriggerProps) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState("");

  // ⌘K / Ctrl+K opens the dialog from anywhere.
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen(true);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  const submit = () => {
    const trimmed = draft.trim();
    if (trimmed.length === 0) return;
    const params = new URLSearchParams({ q: trimmed });
    if (extraEnv) params.set("environment", extraEnv);
    router.push(`/registry/search?${params.toString()}` as Route);
    setOpen(false);
    setDraft("");
  };

  return (
    <>
      {trigger ? (
        <span onClick={() => setOpen(true)} role="presentation">
          {trigger}
        </span>
      ) : (
        <Button
          intent={placement === "registry" ? "secondary" : "ghost"}
          size={placement === "registry" ? "sm" : "icon"}
          onClick={() => setOpen(true)}
          aria-label="Search the registry"
          className={className}
          data-testid="registry-global-search-trigger"
        >
          <Search aria-hidden="true" />
          {placement === "registry" ? <span className="ms-1">Search</span> : null}
        </Button>
      )}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent
          data-testid="registry-global-search-dialog"
          className="max-w-2xl gap-4"
        >
          <DialogTitle>Search the registry</DialogTitle>
          <DialogDescription>
            Press Enter to open the full search page with these results.
          </DialogDescription>
          <RegistrySearchInput
            value={draft}
            onChange={(next) => setDraft(next)}
            autoFocus
          />
          <div className="flex justify-end gap-2">
            <Button intent="secondary" size="sm" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button intent="primary" size="sm" onClick={submit} disabled={draft.trim().length === 0}>
              Search
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
