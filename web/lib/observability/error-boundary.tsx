/**
 * Top-level error boundary (FR-036 / SC-015).
 *
 * Captures unhandled rendering errors with the React component stack,
 * forwards through the active observability adapter, and renders an
 * on-brand accessible error surface.
 */

"use client";

import { Component, type ErrorInfo, type ReactNode } from "react";
import Link from "next/link";

import { t } from "@/lib/i18n";
import { cn } from "@/lib/design-system";
import { getAdapter, type ErrorEventAttributes } from "./adapter";
import { newTraceContext } from "@/lib/http/trace-context";

interface ErrorBoundaryProps {
  readonly children: ReactNode;
  /** Overrides the default fallback if a feature needs a tailored surface. */
  readonly fallback?: (state: ErrorBoundaryState, retry: () => void) => ReactNode;
}

export interface ErrorBoundaryState {
  readonly hasError: boolean;
  readonly message?: string;
  readonly componentStack?: string;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, message: error.message };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    const stack = info.componentStack ?? undefined;
    if (stack) this.setState({ componentStack: stack });
    const adapter = getAdapter();
    const attributes: ErrorEventAttributes = {
      message: error.message,
      category: "render",
      ...(stack ? { componentStack: stack } : {}),
      ...(typeof window !== "undefined" ? { route: window.location.pathname } : {}),
    };
    adapter.capture({
      kind: "error",
      trace: newTraceContext(),
      attributes,
    });
  }

  private readonly retry = (): void => {
    this.setState({ hasError: false });
  };

  override render(): ReactNode {
    if (!this.state.hasError) return this.props.children;
    if (this.props.fallback) return this.props.fallback(this.state, this.retry);
    return <DefaultErrorSurface onRetry={this.retry} />;
  }
}

function DefaultErrorSurface({ onRetry }: { readonly onRetry: () => void }): ReactNode {
  return (
    <div
      role="alert"
      aria-live="assertive"
      className={cn(
        "flex min-h-[60vh] w-full items-center justify-center",
        "bg-surface-canvas text-foreground-default",
      )}
    >
      <div
        className={cn(
          "max-w-prose rounded-lg border border-border-default bg-surface-elevated",
          "p-8 shadow-elevation-2",
        )}
      >
        <h1 className="text-2xl font-semibold">{t("error.boundary.title")}</h1>
        <p className="mt-3 text-foreground-muted">
          {t("error.boundary.description")}
        </p>
        <div className="mt-6 flex flex-wrap gap-3">
          <button
            type="button"
            onClick={onRetry}
            className={cn(
              "inline-flex items-center justify-center rounded-md",
              "bg-accent-primary text-accent-primary-foreground",
              "px-4 py-2 text-sm font-medium",
              "hover:bg-accent-hover active:bg-accent-active",
            )}
          >
            {t("error.boundary.retry")}
          </button>
          <Link
            href="/"
            className={cn(
              "inline-flex items-center justify-center rounded-md",
              "border border-border-default bg-surface-elevated",
              "px-4 py-2 text-sm font-medium text-foreground-default",
              "hover:bg-interactive-hover",
            )}
          >
            {t("error.boundary.home")}
          </Link>
        </div>
      </div>
    </div>
  );
}
