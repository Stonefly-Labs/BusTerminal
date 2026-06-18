/**
 * Spec 009 / T075 / US2 / FR-024.
 *
 * Read-only display for an entity's Azure-sourced technical attributes,
 * rendered per entity type so each shape's relevant fields surface
 * naturally. The data is whatever `azureSourced` happens to contain on the
 * PublishedEntity document — we treat unknown / missing fields as
 * "Unknown" rather than throwing (Spec 009 edge case: rule entities with
 * neither filter nor action expressions render every field as "Unknown").
 *
 * Server component — no client state. Renders into a `<Card>` so the
 * Azure-sourced metadata is visually distinct from the registry-curated
 * metadata card that the existing detail page already mounts.
 */

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type {
  EntityType,
  PublishedEntity,
} from "@/lib/discovery/schemas";

export interface EntityAzureMetadataProps {
  readonly entity: Pick<PublishedEntity, "entityType" | "azureSourced">;
}

const UNKNOWN = "Unknown";

export function EntityAzureMetadata({ entity }: EntityAzureMetadataProps) {
  const fields = entityFieldsFor(entity.entityType, entity.azureSourced);

  return (
    <Card data-testid="entity-azure-metadata" data-entity-type={entity.entityType}>
      <CardHeader>
        <CardTitle>Azure metadata</CardTitle>
        <p className="text-xs text-foreground-muted">
          Authoritative values discovered from Azure Service Bus.
        </p>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
          {fields.map(({ label, value, testId }) => (
            <div key={label} className="flex flex-col gap-0.5">
              <dt className="text-xs font-medium uppercase tracking-wide text-foreground-subtle">
                {label}
              </dt>
              <dd
                data-testid={testId}
                className="text-sm font-medium text-foreground-default break-all"
              >
                {value}
              </dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  );
}

interface FieldRow {
  readonly label: string;
  readonly value: string;
  readonly testId: string;
}

function entityFieldsFor(
  entityType: EntityType,
  azureSourced: Record<string, unknown>,
): FieldRow[] {
  const common: FieldRow[] = [
    { label: "Status", value: str(azureSourced.status), testId: "azure-status" },
    {
      label: "Resource ID",
      value: str(azureSourced.azureResourceId),
      testId: "azure-resource-id",
    },
  ];

  switch (entityType) {
    case "Queue":
      return [
        ...common,
        { label: "Lock duration", value: str(azureSourced.lockDuration), testId: "azure-lock-duration" },
        { label: "Max delivery count", value: str(azureSourced.maxDeliveryCount), testId: "azure-max-delivery-count" },
        {
          label: "Duplicate detection",
          value: durationDetail(azureSourced.duplicateDetection, "historyTimeWindow"),
          testId: "azure-duplicate-detection",
        },
        {
          label: "Dead-letter on expiration",
          value: boolField(azureSourced.deadLettering, "deadLetterOnMessageExpiration"),
          testId: "azure-dead-letter",
        },
        { label: "Partitioning", value: boolField(azureSourced.partitioning, "enabled"), testId: "azure-partitioning" },
        { label: "Session enabled", value: boolField(azureSourced.session, "enabled"), testId: "azure-session" },
        {
          label: "Forward to",
          value: str(getNested(azureSourced.forwarding, "forwardTo")),
          testId: "azure-forward-to",
        },
        { label: "Default TTL", value: str(azureSourced.defaultTimeToLive), testId: "azure-default-ttl" },
        {
          label: "Max size (MB)",
          value: str(azureSourced.maxSizeInMegabytes),
          testId: "azure-max-size",
        },
      ];
    case "Topic":
      return [
        ...common,
        {
          label: "Duplicate detection",
          value: durationDetail(azureSourced.duplicateDetection, "historyTimeWindow"),
          testId: "azure-duplicate-detection",
        },
        { label: "Partitioning", value: boolField(azureSourced.partitioning, "enabled"), testId: "azure-partitioning" },
        { label: "Default TTL", value: str(azureSourced.defaultTimeToLive), testId: "azure-default-ttl" },
        {
          label: "Max size (MB)",
          value: str(azureSourced.maxSizeInMegabytes),
          testId: "azure-max-size",
        },
      ];
    case "Subscription":
      return [
        ...common,
        { label: "Lock duration", value: str(azureSourced.lockDuration), testId: "azure-lock-duration" },
        { label: "Max delivery count", value: str(azureSourced.maxDeliveryCount), testId: "azure-max-delivery-count" },
        {
          label: "Dead-letter on expiration",
          value: boolField(azureSourced.deadLettering, "deadLetterOnMessageExpiration"),
          testId: "azure-dead-letter",
        },
        { label: "Session enabled", value: boolField(azureSourced.session, "enabled"), testId: "azure-session" },
        {
          label: "Forward to",
          value: str(getNested(azureSourced.forwarding, "forwardTo")),
          testId: "azure-forward-to",
        },
        { label: "Default TTL", value: str(azureSourced.defaultTimeToLive), testId: "azure-default-ttl" },
      ];
    case "Rule":
      return [
        ...common,
        { label: "Filter type", value: str(azureSourced.filterType), testId: "azure-filter-type" },
        { label: "Filter expression", value: str(azureSourced.filterExpression), testId: "azure-filter-expression" },
        { label: "Action expression", value: str(azureSourced.actionExpression), testId: "azure-action-expression" },
      ];
  }
}

function str(value: unknown): string {
  if (value === null || value === undefined || value === "") return UNKNOWN;
  if (typeof value === "boolean") return value ? "Enabled" : "Disabled";
  return String(value);
}

function boolField(record: unknown, key: string): string {
  const v = getNested(record, key);
  if (v === undefined || v === null) return UNKNOWN;
  return v === true ? "Enabled" : "Disabled";
}

function durationDetail(record: unknown, key: string): string {
  const enabled = getNested(record, "enabled");
  if (enabled === undefined || enabled === null) return UNKNOWN;
  if (enabled === false) return "Disabled";
  const window = getNested(record, key);
  return window ? `Enabled (${String(window)})` : "Enabled";
}

function getNested(record: unknown, key: string): unknown {
  if (record !== null && typeof record === "object" && key in (record as object)) {
    return (record as Record<string, unknown>)[key];
  }
  return undefined;
}
