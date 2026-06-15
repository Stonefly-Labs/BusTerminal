"use client";

/**
 * Spec 008 / T114 / US2. Client Component that drives the namespace details
 * page — fetches the joined details payload (namespace + latestValidationRun
 * + recentAuditEvents) via TanStack Query and composes the four detail
 * panels.
 */

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, PencilLine } from "lucide-react";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useHasRole } from "@/hooks/use-has-role";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

import { LifecycleStatusBadge } from "../inventory/lifecycle-status-badge";
import { ValidationStatusBadge } from "../inventory/validation-status-badge";
import { NamespaceAuditPanel } from "./namespace-audit-panel";
import { NamespaceMetadataPanel } from "./namespace-metadata-panel";
import { NamespaceOwnershipPanel } from "./namespace-ownership-panel";
import { NamespaceValidationPanel } from "./namespace-validation-panel";

interface NamespaceDetailsProps {
  readonly id: string;
}

export function NamespaceDetails({ id }: NamespaceDetailsProps) {
  const isAdmin = useHasRole("BusTerminal.NamespaceAdministrator");
  const getToken = useAcquireToken();

  const query = useQuery({
    queryKey: namespaceKeys.details(id),
    queryFn: async () => {
      const token = await getToken();
      return NamespacesApi.getDetails(id, token ? { accessToken: token } : {});
    },
  });

  if (query.isLoading) {
    return (
      <div className="p-6">
        <p className="text-sm text-foreground-muted">Loading namespace details…</p>
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="p-6">
        <Card>
          <CardContent className="p-6">
            <p className="text-sm text-error-foreground">
              Failed to load this namespace. Try again, and reach out to the platform team if the error
              persists.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!query.data) {
    return (
      <div className="p-6">
        <Card>
          <CardContent className="flex flex-col gap-3 p-6">
            <h2 className="text-lg font-semibold text-foreground-default">Namespace not found</h2>
            <p className="text-sm text-foreground-muted">
              This namespace either does not exist or you do not have access to it.
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

  return (
    <div className="flex flex-col gap-4 p-6">
      <Link
        href={"/namespaces" as never}
        className="inline-flex items-center gap-1 text-xs text-foreground-muted hover:text-foreground-default"
      >
        <ChevronLeft className="size-3" aria-hidden="true" />
        Back to inventory
      </Link>

      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold text-foreground-default">
            {details.displayName ?? details.name}
          </h1>
          <div className="flex flex-wrap items-center gap-2 text-sm text-foreground-muted">
            <Badge intent="outline">{details.environment}</Badge>
            <LifecycleStatusBadge status={details.lifecycleStatus ?? null} />
            <ValidationStatusBadge status={details.validationStatus ?? null} />
            {details.source === "Onboarded" ? <Badge intent="accent">Onboarded</Badge> : null}
          </div>
        </div>
        {isAdmin ? (
          <div className="flex items-center gap-2">
            <Button asChild intent="secondary">
              <Link href={`/namespaces/${details.id}/edit` as never}>
                <PencilLine className="me-1 size-4" aria-hidden="true" />
                Edit
              </Link>
            </Button>
            <Button asChild intent="outline">
              <Link href={`/namespaces/${details.id}/lifecycle` as never}>Lifecycle</Link>
            </Button>
          </div>
        ) : null}
      </header>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <NamespaceMetadataPanel namespace={details} />
        <NamespaceOwnershipPanel
          ownership={details.ownership ?? null}
          onboardingActor={details.onboardingActor ?? null}
        />
        <NamespaceValidationPanel run={details.latestValidationRun ?? null} />
        <NamespaceAuditPanel events={details.recentAuditEvents} />
      </div>
    </div>
  );
}
