"use client";

import { toast } from "@/components/ui/toast";

/**
 * `useToast` is a thin pass-through over Sonner's imperative `toast` API
 * so consumers don't have to reach across packages and so a future
 * implementation swap stays internal.
 *
 * Usage:
 *   const { toast } = useToast();
 *   toast.success("Saved");
 */
export function useToast() {
  return { toast };
}
