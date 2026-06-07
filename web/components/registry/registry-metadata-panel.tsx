/**
 * Spec 006 / T089. Renders the canonical-field key/value list for a registry
 * entity. Empty/null fields render as a muted placeholder per the
 * spec's "Missing optional metadata" edge case.
 */

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/design-system/cn";
import type { RegistryEntity } from "@/lib/registry/types";

import { RegistryStatusBadge } from "./registry-status-badge";

interface RegistryMetadataPanelProps {
  readonly entity: RegistryEntity;
  readonly className?: string;
}

export function RegistryMetadataPanel({ entity, className }: RegistryMetadataPanelProps) {
  const tags = entity.tags ?? [];

  return (
    <Card className={cn(className)} data-testid="registry-metadata-panel">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <span>{entity.name}</span>
          <RegistryStatusBadge status={entity.status} />
        </CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-1 gap-3 sm:grid-cols-[180px_1fr]">
          <Field label="Entity type" value={entity.entityType} />
          <Field label="Environment" value={entity.environment} />
          <Field label="Owner" value={entity.owner} placeholder="No owner recorded" />
          <Field
            label="Fully-qualified name"
            value={entity.fullyQualifiedName}
            placeholder="—"
            mono
          />
          <Field
            label="Azure resource id"
            value={entity.azureResourceId}
            placeholder="—"
            mono
          />
          <Field label="Description" value={entity.description} placeholder="No description" />
          <Field label="Created" value={entity.createdAtUtc} mono />
          <Field label="Updated" value={entity.updatedAtUtc} mono />
          <Field label="Source" value={entity.source} />
          <Field
            label="Parent id"
            value={entity.parentId}
            placeholder={entity.entityType === "Namespace" ? "—" : "(unknown)"}
            mono
          />
          <div className="contents">
            <dt className="text-xs font-medium uppercase tracking-wide text-foreground-subtle">
              Tags
            </dt>
            <dd className="text-sm">
              {tags.length === 0 ? (
                <span className="text-foreground-muted">No tags</span>
              ) : (
                <ul className="flex flex-wrap gap-2">
                  {tags.map((tag, idx) => (
                    <li
                      key={`${tag.key}-${idx}`}
                      className="rounded-full border border-border-default bg-surface-muted px-2.5 py-0.5 text-xs"
                    >
                      <span className="font-medium">{tag.key}</span>
                      <span className="text-foreground-muted">: {tag.value}</span>
                    </li>
                  ))}
                </ul>
              )}
            </dd>
          </div>
        </dl>
      </CardContent>
    </Card>
  );
}

function Field({
  label,
  value,
  placeholder,
  mono,
}: {
  label: string;
  value: string | null | undefined;
  placeholder?: string;
  mono?: boolean;
}) {
  const empty = value === null || value === undefined || value === "";
  return (
    <div className="contents">
      <dt className="text-xs font-medium uppercase tracking-wide text-foreground-subtle">
        {label}
      </dt>
      <dd className={cn("text-sm", mono && "font-mono", empty && "text-foreground-muted")}>
        {empty ? placeholder ?? "—" : value}
      </dd>
    </div>
  );
}
