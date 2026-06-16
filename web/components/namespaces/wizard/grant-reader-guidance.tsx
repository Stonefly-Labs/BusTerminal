"use client";

/**
 * Spec 008 / T084 + research §4 / §17. Step-1 sidebar that surfaces a
 * copy-pasteable `az role assignment create` command for the operator to
 * grant Reader to BusTerminal's workload UAMI on the namespace they're
 * onboarding. Two states:
 *   - Empty ARM id: render template with `{azureResourceId}` placeholder
 *     + a hint to paste an ARM id above.
 *   - Populated ARM id: substitute the live value reactively (debounced).
 */

import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Copy, ExternalLink, ShieldCheck } from "lucide-react";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

export interface GrantReaderGuidanceProps {
  readonly azureResourceId: string;
}

const PLACEHOLDER = "{azureResourceId}";

export function GrantReaderGuidance({ azureResourceId }: GrantReaderGuidanceProps) {
  const getToken = useAcquireToken();
  const identity = useQuery({
    queryKey: namespaceKeys.identity(),
    queryFn: async () => {
      const token = await getToken();
      return NamespacesApi.getIdentity(token ? { accessToken: token } : {});
    },
    staleTime: 1000 * 60 * 30,
  });

  const debouncedArmId = useDebounced(azureResourceId.trim(), 200);

  const command = useMemo(() => {
    if (!identity.data) return null;
    const template = identity.data.sampleGrantCommand
      ?? `az role assignment create --assignee ${identity.data.principalId} --role Reader --scope ${PLACEHOLDER}`;
    if (!debouncedArmId) {
      return template;
    }
    return template.replace(PLACEHOLDER, debouncedArmId);
  }, [identity.data, debouncedArmId]);

  const [copied, setCopied] = useState(false);
  useEffect(() => {
    if (!copied) return;
    const id = window.setTimeout(() => setCopied(false), 1500);
    return () => window.clearTimeout(id);
  }, [copied]);

  return (
    <Card data-testid="grant-reader-guidance">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-sm">
          <ShieldCheck className="h-4 w-4" aria-hidden="true" />
          Grant Reader to BusTerminal
        </CardTitle>
        <CardDescription>
          BusTerminal needs the built-in <strong>Reader</strong> role on this namespace before
          validation can run. Grant it via the Azure CLI below.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {identity.isPending ? (
          <p className="text-xs text-foreground-muted">Resolving workload identity&hellip;</p>
        ) : identity.isError ? (
          <p className="text-xs text-error-foreground">
            Workload identity unavailable. See the deployment runbook.
          </p>
        ) : (
          <>
            <pre
              className="overflow-x-auto rounded-md bg-surface-muted p-3 font-mono text-xs"
              data-testid="grant-reader-guidance-command"
            >
              {command}
            </pre>
            {!debouncedArmId ? (
              <p className="text-xs text-foreground-muted">
                Paste an ARM id above to populate the scope.
              </p>
            ) : null}
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                size="sm"
                intent="outline"
                disabled={!command}
                onClick={async () => {
                  if (!command) return;
                  await navigator.clipboard.writeText(command);
                  setCopied(true);
                }}
                aria-label="Copy az role assignment command"
                data-testid="grant-reader-guidance-copy"
              >
                <Copy className="me-1 h-3.5 w-3.5" aria-hidden="true" />
                {copied ? "Copied!" : "Copy command"}
              </Button>
              {identity.data?.runbookUrl ? (
                <Button asChild type="button" size="sm" intent="link">
                  <a
                    href={identity.data.runbookUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    data-testid="grant-reader-guidance-runbook"
                  >
                    Runbook
                    <ExternalLink className="ms-1 h-3.5 w-3.5" aria-hidden="true" />
                  </a>
                </Button>
              ) : null}
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function useDebounced<T>(value: T, ms: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), ms);
    return () => window.clearTimeout(id);
  }, [value, ms]);
  return debounced;
}
