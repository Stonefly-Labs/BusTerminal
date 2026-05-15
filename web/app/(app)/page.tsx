"use client";

import * as React from "react";

import { Breadcrumbs } from "@/components/navigation/breadcrumb";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { DataTable } from "@/components/data-table/data-table";
import { Field } from "@/components/forms/field";
import { Form } from "@/components/forms/form";
import { PageContainer } from "@/components/app-shell/page-container";
import { PageHeader } from "@/components/app-shell/page-header";
import { SectionContainer } from "@/components/app-shell/section-container";
import { ChartLine } from "@/components/charts/chart-line";
import { useToast } from "@/hooks/use-toast";

import { AzureResourceLink } from "@/components/domain/azure-resource-link";
import { DeadLetterIndicator } from "@/components/domain/dead-letter-indicator";
import { DiscoveryJobStatus } from "@/components/domain/discovery-job-status";
import { EntityRelationshipBadge } from "@/components/domain/entity-relationship-badge";
import { EnvironmentBadge } from "@/components/domain/environment-badge";
import { HealthSummaryIndicator } from "@/components/domain/health-summary-indicator";
import { MessageCountIndicator } from "@/components/domain/message-count-indicator";
import { MetadataKeyValuePanel } from "@/components/domain/metadata-key-value-panel";
import { NamespaceCard } from "@/components/domain/namespace-card";
import { QueueCard } from "@/components/domain/queue-card";
import { QueueRow as QueueDomainRow } from "@/components/domain/queue-row";
import { SubscriptionCard } from "@/components/domain/subscription-card";
import { SubscriptionRow as SubscriptionDomainRow } from "@/components/domain/subscription-row";
import { TopicCard } from "@/components/domain/topic-card";
import { TopicRow as TopicDomainRow } from "@/components/domain/topic-row";
import { TopologyMiniMapPlaceholder } from "@/components/domain/topology-mini-map-placeholder";

import { SHOWCASE_COLUMNS, SHOWCASE_QUEUES, type QueueRow } from "./_showcase/showcase-data";
import { newQueueSchema, type NewQueueValues } from "./_showcase/showcase-schemas";
import {
  SHOWCASE_AZURE_RESOURCE_ID,
  SHOWCASE_DOMAIN_QUEUE,
  SHOWCASE_DOMAIN_QUEUE_DLQ,
  SHOWCASE_JOB_STARTED_AT,
  SHOWCASE_METADATA,
  SHOWCASE_NAMESPACE,
  SHOWCASE_NOW,
  SHOWCASE_PORTAL_URL,
  SHOWCASE_SUBSCRIPTION,
  SHOWCASE_TOPIC,
} from "./_showcase/domain-showcase-data";

const TIMELINE = [
  { hour: "00:00", active: 12, dl: 0 },
  { hour: "04:00", active: 18, dl: 1 },
  { hour: "08:00", active: 42, dl: 2 },
  { hour: "12:00", active: 88, dl: 1 },
  { hour: "16:00", active: 60, dl: 4 },
  { hour: "20:00", active: 30, dl: 0 },
];

export default function FoundationDemoPage() {
  const { toast } = useToast();
  const [drawerOpen, setDrawerOpen] = React.useState(false);
  const [activeRow, setActiveRow] = React.useState<QueueRow | null>(null);

  const enrichedColumns = React.useMemo(
    () => [
      ...SHOWCASE_COLUMNS,
      {
        id: "actions",
        header: "",
        cell: (info: { row: { original: QueueRow } }) => (
          <Button
            intent="ghost"
            size="sm"
            onClick={() => {
              setActiveRow(info.row.original);
              setDrawerOpen(true);
            }}
          >
            Details
          </Button>
        ),
      },
    ],
    [],
  );

  return (
    <PageContainer>
      <PageHeader
        title="Foundation showcase"
        description="A composed operational screen that exercises every foundation primitive."
        breadcrumb={
          <Breadcrumbs
            crumbs={[
              { id: "root", label: "Dashboard", href: "/" },
              { id: "showcase", label: "Foundation showcase" },
            ]}
          />
        }
        actions={<Button>Refresh</Button>}
      />

      <SectionContainer>
        <h2 className="text-sm font-semibold uppercase tracking-wide text-foreground-muted">
          Hourly throughput
        </h2>
        <div className="rounded-md border border-border-default bg-surface-elevated p-3">
          <ChartLine
            data={TIMELINE}
            xKey="hour"
            series={[
              { id: "active", accessor: "active", label: "Active" },
              { id: "dl", accessor: "dl", label: "Dead-letter" },
            ]}
            accessibleLabel="Hourly active and dead-letter message counts"
            height={220}
          />
        </div>
      </SectionContainer>

      <SectionContainer>
        <h2 className="text-sm font-semibold uppercase tracking-wide text-foreground-muted">
          Queues
        </h2>
        <DataTable
          columns={enrichedColumns}
          data={SHOWCASE_QUEUES}
          getRowId={(row) => row.id}
          caption="Queues in orders-westus"
          searchColumnId="name"
          paginationMode="paginated"
          persistenceKey="showcase-queues"
        />
      </SectionContainer>

      <SectionContainer>
        <h2 className="text-sm font-semibold uppercase tracking-wide text-foreground-muted">
          Add queue
        </h2>
        <div className="rounded-md border border-border-default bg-surface-elevated p-4">
          <Form
            schema={newQueueSchema}
            defaultValues={{ name: "", maxDelivery: 10, description: "" }}
            accessibleNameKey="form.submit.default"
            onSubmit={async (values: NewQueueValues) => {
              await new Promise((resolve) => setTimeout(resolve, 250));
              toast.success(`Queue ${values.name} queued for provisioning`);
            }}
          >
            {(form) => (
              <div className="grid gap-4 md:grid-cols-2">
                <Field control={form.control} name="name" labelKey="domain.queue.label" required>
                  <Input placeholder="orders.in" />
                </Field>
                <Field control={form.control} name="maxDelivery" labelKey="form.submit.default" required>
                  <Input type="number" min={1} max={100} />
                </Field>
                <div className="md:col-span-2">
                  <Field control={form.control} name="description" labelKey="form.submit.default">
                    <Textarea placeholder="Optional description" rows={3} />
                  </Field>
                </div>
                <div className="md:col-span-2 flex justify-end">
                  <Button type="submit" disabled={form.formState.isSubmitting}>
                    {form.formState.isSubmitting ? "Saving" : "Save queue"}
                  </Button>
                </div>
              </div>
            )}
          </Form>
        </div>
      </SectionContainer>

      <SectionContainer>
        <h2 className="text-sm font-semibold uppercase tracking-wide text-foreground-muted">
          Domain composites
        </h2>
        <p className="text-xs text-foreground-muted">
          Every BusTerminal-specific composite from FR-028 rendered with realistic sample data.
        </p>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          <NamespaceCard namespace={SHOWCASE_NAMESPACE} />
          <QueueCard queue={SHOWCASE_DOMAIN_QUEUE} />
          <QueueCard queue={SHOWCASE_DOMAIN_QUEUE_DLQ} />
          <TopicCard topic={SHOWCASE_TOPIC} />
          <SubscriptionCard subscription={SHOWCASE_SUBSCRIPTION} />
        </div>

        <div className="flex flex-col gap-2">
          <QueueDomainRow queue={SHOWCASE_DOMAIN_QUEUE} />
          <QueueDomainRow queue={SHOWCASE_DOMAIN_QUEUE_DLQ} />
          <TopicDomainRow topic={SHOWCASE_TOPIC} />
          <SubscriptionDomainRow subscription={SHOWCASE_SUBSCRIPTION} />
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <EnvironmentBadge environment="dev" />
          <EnvironmentBadge environment="test" />
          <EnvironmentBadge environment="staging" />
          <EnvironmentBadge environment="prod" />
          <DeadLetterIndicator count={0} />
          <DeadLetterIndicator count={14} />
          <MessageCountIndicator count={1_240} />
          <HealthSummaryIndicator counts={{ healthy: 18, degraded: 3, unhealthy: 0 }} />
          <DiscoveryJobStatus
            state="running"
            startedAt={SHOWCASE_JOB_STARTED_AT}
            now={SHOWCASE_NOW}
          />
          <EntityRelationshipBadge
            kind="subscribes"
            from="billing-pipeline"
            to="orders.events"
          />
        </div>

        <AzureResourceLink
          resourceId={SHOWCASE_AZURE_RESOURCE_ID}
          label="orders-westus"
          portalUrl={SHOWCASE_PORTAL_URL}
        />

        <MetadataKeyValuePanel entries={[...SHOWCASE_METADATA]} />

        <TopologyMiniMapPlaceholder />
      </SectionContainer>

      <Sheet open={drawerOpen} onOpenChange={setDrawerOpen}>
        <SheetContent>
          <SheetHeader>
            <SheetTitle>{activeRow?.name ?? "Queue"}</SheetTitle>
            <SheetDescription>
              {activeRow ? `Status: ${activeRow.status}` : "Select a queue to view details"}
            </SheetDescription>
          </SheetHeader>
          {activeRow ? (
            <dl className="mt-4 grid grid-cols-2 gap-3 text-sm">
              <dt className="text-foreground-muted">Active</dt>
              <dd className="font-mono">{activeRow.active}</dd>
              <dt className="text-foreground-muted">Dead-letter</dt>
              <dd className="font-mono">{activeRow.deadLetter}</dd>
              <dt className="text-foreground-muted">Max delivery</dt>
              <dd className="font-mono">{activeRow.maxDelivery}</dd>
            </dl>
          ) : null}
        </SheetContent>
      </Sheet>
    </PageContainer>
  );
}
