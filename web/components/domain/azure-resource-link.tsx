"use client";

import * as React from "react";
import { Check, Copy, ExternalLink } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n/strings";
import { getDomainIcon } from "@/lib/iconography/domain-icons";

export interface AzureResourceLinkProps
  extends Omit<React.HTMLAttributes<HTMLDivElement>, "children" | "title"> {
  /** Azure ARM resource ID (e.g., `/subscriptions/<sub>/resourceGroups/<rg>/…`). */
  readonly resourceId: string;
  /** Optional human-friendly label. Falls back to the resourceId. */
  readonly label?: string;
  /** Azure portal URL the external-link affordance navigates to. */
  readonly portalUrl: string;
}

export const AzureResourceLink = React.forwardRef<HTMLDivElement, AzureResourceLinkProps>(
  function AzureResourceLink({ resourceId, label, portalUrl, className, ...rest }, ref) {
    const { icon: AzureIcon, strokeWidth } = getDomainIcon("azure-resource");
    const [copied, setCopied] = React.useState(false);

    const handleCopy = React.useCallback(async () => {
      if (typeof navigator === "undefined" || !navigator.clipboard) return;
      try {
        await navigator.clipboard.writeText(resourceId);
        setCopied(true);
        window.setTimeout(() => setCopied(false), 1_400);
      } catch {
        // Clipboard refused — keep the affordance silent; the toast surface is
        // the right channel for feedback in consuming pages.
      }
    }, [resourceId]);

    return (
      <div
        ref={ref}
        className={cn(
          "inline-flex max-w-full items-center gap-2 rounded-md border border-border-default bg-surface-muted px-2 py-1 text-sm",
          className,
        )}
        {...rest}
      >
        <AzureIcon
          aria-hidden="true"
          strokeWidth={strokeWidth}
          className="size-4 shrink-0 text-foreground-muted"
        />
        <span
          className="min-w-0 truncate font-mono text-foreground-default"
          title={resourceId}
        >
          {label ?? resourceId}
        </span>
        <Button
          intent="ghost"
          size="icon"
          type="button"
          onClick={handleCopy}
          aria-label={t("domain.azureResource.copyLabel")}
        >
          {copied ? (
            <Check aria-hidden="true" />
          ) : (
            <Copy aria-hidden="true" />
          )}
        </Button>
        <Button intent="ghost" size="icon" asChild>
          <a
            href={portalUrl}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={t("domain.azureResource.openLabel")}
          >
            <ExternalLink aria-hidden="true" />
          </a>
        </Button>
      </div>
    );
  },
);
