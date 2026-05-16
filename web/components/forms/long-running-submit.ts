"use client";

import { useCallback } from "react";
import type { FieldValues, SubmitHandler } from "react-hook-form";

import { toast } from "@/components/ui/toast";
import { t } from "@/lib/i18n";
import type { StringKey } from "@/lib/i18n";
import { getAdapter } from "@/lib/observability/adapter";
import { newTraceContext } from "@/lib/http/trace-context";

export interface UseLongRunningSubmitOptions<TValues> {
  readonly onSubmit: SubmitHandler<TValues>;
  readonly progressLabelKey: StringKey;
  readonly successLabelKey: StringKey;
  readonly errorCategory: "data-fetch" | "render" | "route-load";
}

/**
 * Wrap a submit handler so progress, success, and failure surface through
 * the toast surface (FR-018) and route through the observability adapter
 * (FR-040). Returns a SubmitHandler suitable for `form.handleSubmit(...)`.
 */
export function useLongRunningSubmit<TValues extends FieldValues>(
  options: UseLongRunningSubmitOptions<TValues>,
): SubmitHandler<TValues> {
  const { onSubmit, progressLabelKey, successLabelKey, errorCategory } = options;
  return useCallback<SubmitHandler<TValues>>(
    async (values) => {
      const toastId = toast.loading(t(progressLabelKey));
      try {
        await onSubmit(values);
        toast.success(t(successLabelKey), { id: toastId });
      } catch (error) {
        toast.error(error instanceof Error ? error.message : "Failed", { id: toastId });
        getAdapter().capture({
          kind: "error",
          trace: newTraceContext(),
          attributes: {
            message: error instanceof Error ? error.message : "Submission failed",
            category: errorCategory,
          },
        });
        throw error;
      }
    },
    [onSubmit, progressLabelKey, successLabelKey, errorCategory],
  );
}
