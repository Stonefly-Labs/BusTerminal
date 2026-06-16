/**
 * Spec 008 / T110 / US2. Metadata panel — renders business metadata, Azure
 * identifiers, environment, and tags for a single onboarded namespace.
 *
 * RSC-safe (pure presentational).
 */

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { OnboardedNamespace } from "@/lib/namespaces/types";

interface NamespaceMetadataPanelProps {
  readonly namespace: OnboardedNamespace;
}

export function NamespaceMetadataPanel({ namespace }: NamespaceMetadataPanelProps) {
  return (
    <Card data-testid="namespace-metadata-panel">
      <CardHeader>
        <CardTitle>Metadata</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        <Section title="Business">
          <Row label="Display name" value={namespace.displayName ?? namespace.name} />
          <Row label="Description" value={namespace.description ?? null} />
          <Row label="Business unit" value={namespace.businessUnit ?? null} />
          <Row label="Product / application" value={namespace.productOrApplication ?? null} />
          <Row label="Cost center" value={namespace.costCenter ?? null} />
          <Row label="Environment" value={<Badge intent="outline">{namespace.environment}</Badge>} />
          <Row label="Notes" value={namespace.notes ?? null} multiline />
        </Section>

        <Section title="Azure identifiers">
          <Row label="ARM resource id" value={<code className="font-mono text-xs">{namespace.azureResourceId ?? "—"}</code>} />
          <Row label="Namespace name" value={<code className="font-mono text-xs">{namespace.name}</code>} />
          <Row label="Subscription" value={namespace.subscriptionName ?? namespace.subscriptionId ?? null} />
          <Row label="Resource group" value={namespace.resourceGroup ?? null} />
          <Row label="Region" value={namespace.region ?? null} />
          <Row label="Tenant id" value={<code className="font-mono text-xs">{namespace.tenantId ?? "—"}</code>} />
        </Section>

        {namespace.tags && namespace.tags.length > 0 ? (
          <Section title="Tags">
            <div className="flex flex-wrap gap-1">
              {namespace.tags.map((tag) => (
                <Badge key={`${tag.key}=${tag.value}`} intent="outline">
                  <span className="font-medium">{tag.key}</span>
                  <span className="opacity-60">=</span>
                  {tag.value}
                </Badge>
              ))}
            </div>
          </Section>
        ) : null}
      </CardContent>
    </Card>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-foreground-subtle">{title}</h3>
      <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-[180px_1fr]">{children}</dl>
    </div>
  );
}

function Row({
  label,
  value,
  multiline,
}: {
  label: string;
  value: React.ReactNode | string | null;
  multiline?: boolean;
}) {
  const display: React.ReactNode =
    value === null || value === undefined || value === ""
      ? <span className="text-foreground-muted">—</span>
      : value;
  return (
    <>
      <dt className="text-sm font-medium text-foreground-muted">{label}</dt>
      <dd className={multiline ? "whitespace-pre-wrap text-sm text-foreground-default" : "text-sm text-foreground-default"}>
        {display}
      </dd>
    </>
  );
}
