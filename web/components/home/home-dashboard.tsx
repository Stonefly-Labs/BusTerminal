"use client";

/**
 * Authenticated landing dashboard.
 *
 * Replaces /platform-status as the default sign-in destination. Lays out:
 *   1. Personalized header with display name + visible role badges.
 *   2. Three tile cards (Namespaces, Registry, Platform status) — role-aware
 *      so unauthorized surfaces collapse into a "no access" message rather
 *      than disappear silently.
 *   3. Recent namespace activity panel — top 5 by last validated time. Uses
 *      the spec-008 inventory endpoint so it stays accurate without a new
 *      server route.
 *
 * Composition: existing shadcn primitives (Card, Badge, Button). No new
 * design tokens, no second design system, dark-mode-primary unchanged.
 */

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, Database, Layers, Plus, RotateCcw, Sparkles } from "lucide-react";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useCurrentUser } from "@/hooks/use-current-user";
import { useHasRole } from "@/hooks/use-has-role";
import { useResolvedRoleContext } from "@/components/auth/role-context";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";
import {
  authorizedRoles,
  type OperationClass,
  type PlatformRole,
} from "@/lib/auth/role-permission-matrix";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

import { LifecycleStatusBadge } from "@/components/namespaces/inventory/lifecycle-status-badge";
import { ValidationStatusBadge } from "@/components/namespaces/inventory/validation-status-badge";

const ROLE_LABELS: Record<PlatformRole, string> = {
  "BusTerminal.Admin": "Admin",
  "BusTerminal.Operator": "Operator",
  "BusTerminal.Reader": "Reader",
  "BusTerminal.Developer": "Developer",
  "BusTerminal.NamespaceAdministrator": "Namespace administrator",
};

interface SurfaceTile {
  readonly title: string;
  readonly description: string;
  readonly href: string;
  readonly icon: typeof Database;
  readonly operationClass: OperationClass;
  readonly cta: string;
}

const SURFACES: readonly SurfaceTile[] = [
  {
    title: "Namespaces",
    description:
      "Onboard Azure Service Bus namespaces, manage ownership, transition lifecycle, and re-run validation on demand.",
    href: "/namespaces",
    icon: Layers,
    operationClass: "Read",
    cta: "Open inventory",
  },
  {
    title: "Registry",
    description:
      "Browse the full messaging hierarchy — namespaces, queues, topics, subscriptions, rules — across every environment.",
    href: "/registry",
    icon: Database,
    operationClass: "Read",
    cta: "Browse registry",
  },
  {
    title: "Platform status",
    description:
      "End-to-end diagnostic of sign-in, token validation, trace propagation, and central telemetry for this environment.",
    href: "/platform-status",
    icon: Sparkles,
    operationClass: "Read",
    cta: "View status",
  },
];

export function HomeDashboard() {
  const account = useCurrentUser();
  const { effectiveRoles } = useResolvedRoleContext();
  const canOnboardNamespace = useHasRole("BusTerminal.NamespaceAdministrator");
  const getToken = useAcquireToken();

  const recentNamespaces = useQuery({
    queryKey: [...namespaceKeys.inventory.all, "home-recent", 5] as const,
    queryFn: async () => {
      const token = await getToken();
      return NamespacesApi.listInventory(
        { pageSize: 5, sort: "lastValidatedAt_desc" },
        token ? { accessToken: token } : {},
      );
    },
    staleTime: 30_000,
  });

  const greetingName = account?.name ?? account?.username?.split("@")[0] ?? "there";
  const sortedRoles = [...effectiveRoles].sort();

  return (
    <div className="flex flex-col gap-6" data-testid="home-dashboard">
      <header className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground-default">
          Welcome back, {greetingName}
        </h1>
        <p className="text-sm text-foreground-muted">
          BusTerminal — the authoritative source for your Azure messaging topology.
        </p>
        {sortedRoles.length > 0 ? (
          <div className="mt-1 flex flex-wrap items-center gap-1.5">
            <span className="text-xs uppercase tracking-wide text-foreground-subtle">
              Signed-in as
            </span>
            {sortedRoles.map((role) => (
              <Badge key={role} intent="outline">
                {ROLE_LABELS[role]}
              </Badge>
            ))}
          </div>
        ) : null}
      </header>

      <section aria-labelledby="surfaces-heading" className="flex flex-col gap-3">
        <h2
          id="surfaces-heading"
          className="text-xs font-semibold uppercase tracking-wide text-foreground-subtle"
        >
          Where to go
        </h2>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {SURFACES.map((surface) => {
            const allowed = authorizedRoles(surface.operationClass);
            const hasAccess = allowed.some((r) => effectiveRoles.has(r));
            const Icon = surface.icon;
            return (
              <Card
                key={surface.href}
                data-testid={`home-tile-${surface.href.replace(/^\//, "")}`}
              >
                <CardHeader className="flex flex-row items-center justify-between gap-2">
                  <CardTitle className="flex items-center gap-2 text-base">
                    <Icon aria-hidden="true" className="size-4 text-foreground-muted" />
                    {surface.title}
                  </CardTitle>
                </CardHeader>
                <CardContent className="flex flex-col gap-4">
                  <CardDescription>{surface.description}</CardDescription>
                  {hasAccess ? (
                    <div className="flex flex-wrap items-center gap-2">
                      <Button asChild intent="secondary" size="sm">
                        <Link href={surface.href as never}>
                          {surface.cta}
                          <ArrowRight aria-hidden="true" className="ms-1 size-3.5" />
                        </Link>
                      </Button>
                      {surface.href === "/namespaces" && canOnboardNamespace ? (
                        <Button asChild intent="outline" size="sm">
                          <Link href={"/namespaces/onboard" as never}>
                            <Plus aria-hidden="true" className="me-1 size-3.5" />
                            Onboard a namespace
                          </Link>
                        </Button>
                      ) : null}
                    </div>
                  ) : (
                    <p className="text-xs text-foreground-subtle">
                      Your assigned role does not currently grant access to this surface.
                    </p>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </div>
      </section>

      <RecentNamespacesPanel
        loading={recentNamespaces.isLoading}
        error={recentNamespaces.isError}
        items={recentNamespaces.data?.items ?? []}
        onRetry={() => recentNamespaces.refetch()}
      />
    </div>
  );
}

interface RecentNamespacesPanelProps {
  readonly loading: boolean;
  readonly error: boolean;
  readonly items: ReadonlyArray<{
    readonly id: string;
    readonly name: string;
    readonly displayName?: string | null | undefined;
    readonly environment: string;
    readonly lifecycleStatus?: "Active" | "Disabled" | "Archived" | null | undefined;
    readonly validationStatus?: "Healthy" | "Degraded" | "Unhealthy" | null | undefined;
    readonly lastValidatedAtUtc?: string | null | undefined;
  }>;
  readonly onRetry: () => void;
}

function RecentNamespacesPanel({ loading, error, items, onRetry }: RecentNamespacesPanelProps) {
  return (
    <section aria-labelledby="recent-namespaces-heading">
      <Card data-testid="home-recent-namespaces">
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle id="recent-namespaces-heading" className="text-base">
              Recent namespace activity
            </CardTitle>
            <CardDescription>
              The five most recently validated namespaces across every environment.
            </CardDescription>
          </div>
          {error ? (
            <Button type="button" intent="ghost" size="sm" onClick={onRetry}>
              <RotateCcw aria-hidden="true" className="me-1 size-3.5" />
              Retry
            </Button>
          ) : null}
        </CardHeader>
        <CardContent>
          {loading ? (
            <div
              aria-busy="true"
              aria-live="polite"
              data-testid="home-recent-namespaces-loading"
              className="h-1 w-full animate-pulse rounded bg-border-default"
            />
          ) : error ? (
            <p className="text-sm text-error-foreground">
              Failed to load recent namespaces. Try again, and reach out to the platform team if
              the error persists.
            </p>
          ) : items.length === 0 ? (
            <div className="flex flex-col gap-1">
              <p className="text-sm text-foreground-muted">
                No onboarded namespaces yet.
              </p>
              <p className="text-xs text-foreground-subtle">
                Once a namespace is onboarded it will appear here ordered by last validation time.
              </p>
            </div>
          ) : (
            <ul className="flex flex-col gap-2">
              {items.map((ns) => (
                <li
                  key={ns.id}
                  className="flex flex-col gap-1 rounded border border-border-default px-3 py-2 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="flex flex-col gap-1">
                    <Link
                      href={`/namespaces/${ns.id}` as never}
                      className="text-sm font-medium text-foreground-default hover:underline"
                    >
                      {ns.displayName ?? ns.name}
                    </Link>
                    <div className="flex flex-wrap items-center gap-1.5">
                      <Badge intent="outline">{ns.environment}</Badge>
                      <LifecycleStatusBadge status={ns.lifecycleStatus ?? null} />
                      <ValidationStatusBadge status={ns.validationStatus ?? null} />
                    </div>
                  </div>
                  <p className="text-xs text-foreground-subtle">
                    {ns.lastValidatedAtUtc
                      ? `Last validated ${new Date(ns.lastValidatedAtUtc).toLocaleString()}`
                      : "Never validated"}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </section>
  );
}
