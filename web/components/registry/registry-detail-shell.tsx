/**
 * Spec 006 / T088. Detail-page shell. Composes the metadata panel plus
 * placeholders for the relationships + audit panels (those land in US3).
 * Pure server component — interactive bits sit inside the child components.
 */

import Link from "next/link";
import type { Route } from "next";
import { Pencil } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/design-system/cn";
import type { RegistryEntity } from "@/lib/registry/types";

import { RegistryMetadataPanel } from "./registry-metadata-panel";

interface RegistryDetailShellProps {
  readonly entity: RegistryEntity;
  readonly className?: string;
  readonly relationshipsSlot?: React.ReactNode;
  readonly auditSlot?: React.ReactNode;
}

export function RegistryDetailShell({
  entity,
  className,
  relationshipsSlot,
  auditSlot,
}: RegistryDetailShellProps) {
  const editHref = `/registry/${entity.entityType}/${entity.id}/edit` as Route;

  return (
    <div
      data-testid="registry-detail-shell"
      data-entity-id={entity.id}
      className={cn("flex flex-col gap-4", className)}
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground-default">{entity.name}</h1>
          {entity.fullyQualifiedName ? (
            <p className="font-mono text-xs text-foreground-muted">{entity.fullyQualifiedName}</p>
          ) : null}
        </div>
        <Button asChild intent="secondary" size="sm">
          <Link href={editHref}>
            <Pencil className="me-1 size-4" aria-hidden="true" />
            Edit
          </Link>
        </Button>
      </div>

      <RegistryMetadataPanel entity={entity} />

      <Card>
        <CardHeader>
          <CardTitle>Relationships</CardTitle>
        </CardHeader>
        <CardContent>
          {relationshipsSlot ?? (
            <p className="text-sm text-foreground-muted">
              Children &amp; parent links appear here once User Story 3 lands.
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recent activity</CardTitle>
        </CardHeader>
        <CardContent>
          {auditSlot ?? (
            <p className="text-sm text-foreground-muted">
              Audit history appears here once User Story 3 lands.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
