"use client";

/**
 * Spec 008 / T139 / US3. Client driver for the namespace lifecycle experience.
 *
 * Loads the joined details payload, gates the surface on
 * `BusTerminal.NamespaceAdministrator`, and surfaces the lifecycle
 * transition buttons + dialog.
 */

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft } from "lucide-react";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useHasRole } from "@/hooks/use-has-role";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { LifecycleStatusBadge } from "../inventory/lifecycle-status-badge";
import { LifecycleTransitionButtons } from "./lifecycle-transition-button";

export interface NamespaceLifecycleClientProps {
  readonly id: string;
}

export function NamespaceLifecycleClient({ id }: NamespaceLifecycleClientProps) {
  const isAdmin = useHasRole("BusTerminal.NamespaceAdministrator");
  const getToken = useAcquireToken();
  const router = useRouter();

  const query = useQuery({
    queryKey: namespaceKeys.details(id),
    queryFn: async () => {
      const token = await getToken();
      return NamespacesApi.getDetails(id, token ? { accessToken: token } : {});
    },
  });

  if (!isAdmin) {
    return (
      <div className="p-6">
        <Card>
          <CardContent className="flex flex-col gap-3 p-6">
            <h2 className="text-lg font-semibold text-foreground-default">Permission required</h2>
            <p className="text-sm text-foreground-muted">
              Transitioning namespace lifecycle requires the{" "}
              <code className="font-mono text-xs">BusTerminal.NamespaceAdministrator</code> role.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (query.isLoading) {
    return (
      <div className="p-6">
        <p className="text-sm text-foreground-muted">Loading namespace…</p>
      </div>
    );
  }

  if (query.isError || !query.data) {
    return (
      <div className="p-6">
        <Card>
          <CardContent className="flex flex-col gap-3 p-6">
            <h2 className="text-lg font-semibold text-foreground-default">Namespace unavailable</h2>
            <p className="text-sm text-foreground-muted">
              Could not load the namespace. Return to inventory and try again.
            </p>
            <Button asChild intent="secondary">
              <Link href={"/namespaces" as never}>
                <ChevronLeft className="me-1 size-4" aria-hidden="true" />
                Back to inventory
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const details = query.data.details;
  const etag = query.data.etag;
  const currentStatus = details.lifecycleStatus ?? null;

  return (
    <div className="flex flex-col gap-4 p-6">
      <Link
        href={`/namespaces/${id}` as never}
        className="inline-flex items-center gap-1 text-xs text-foreground-muted hover:text-foreground-default"
      >
        <ChevronLeft className="size-3" aria-hidden="true" />
        Back to details
      </Link>

      <header>
        <h1 className="text-2xl font-semibold text-foreground-default">
          Lifecycle — {details.displayName ?? details.name}
        </h1>
        <div className="mt-2 flex items-center gap-2 text-sm text-foreground-muted">
          Current state: <LifecycleStatusBadge status={currentStatus} />
        </div>
      </header>

      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Available transitions</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {currentStatus === null ? (
            <p className="text-sm text-foreground-muted">
              Lifecycle status is unavailable for this namespace.
            </p>
          ) : (
            <LifecycleTransitionButtons
              namespaceId={details.id}
              currentStatus={currentStatus}
              etag={etag}
              onSuccess={() => router.push(`/namespaces/${id}` as never)}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
