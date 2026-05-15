"use client";

import { useEffect } from "react";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { ErrorState } from "@/components/feedback/error-state";
import { getAdapter } from "@/lib/observability/adapter";
import { newTraceContext } from "@/lib/http/trace-context";
import { t } from "@/lib/i18n";

interface AppErrorProps {
  readonly error: Error & { digest?: string };
  readonly reset: () => void;
}

export default function AppError({ error, reset }: AppErrorProps) {
  useEffect(() => {
    getAdapter().capture({
      kind: "error",
      trace: newTraceContext(),
      attributes: {
        message: error.message,
        category: "render",
        ...(error.stack ? { componentStack: error.stack } : {}),
      },
    });
  }, [error]);

  return (
    <div className="mx-auto flex w-full max-w-screen-md flex-col gap-4 p-6">
      <ErrorState
        titleKey="error.boundary.title"
        descriptionKey="error.boundary.description"
        action={
          <div className="flex gap-2">
            <Button intent="secondary" onClick={reset}>
              {t("error.boundary.retry")}
            </Button>
            <Button intent="ghost" asChild>
              <Link href="/">{t("error.boundary.home")}</Link>
            </Button>
          </div>
        }
      />
    </div>
  );
}
