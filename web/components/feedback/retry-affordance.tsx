"use client";

import { RefreshCw } from "lucide-react";

import { Button } from "@/components/ui/button";
import { t } from "@/lib/i18n";

export interface RetryAffordanceProps {
  readonly onRetry: () => void;
  readonly disabled?: boolean;
}

export function RetryAffordance({ onRetry, disabled }: RetryAffordanceProps) {
  return (
    <Button intent="secondary" size="sm" onClick={onRetry} disabled={disabled}>
      <RefreshCw className="size-3.5" aria-hidden="true" />
      {t("feedback.retry.label")}
    </Button>
  );
}
