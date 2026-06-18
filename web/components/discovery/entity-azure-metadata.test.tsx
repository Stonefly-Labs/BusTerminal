/**
 * Spec 009 / T066 / US2. Component test for `<EntityAzureMetadata>`.
 * Covers: per-entity-type field rendering, "Unknown" fallback for the rule
 * edge case (missing filter + action), and shared fields.
 */

import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import { EntityAzureMetadata } from "./entity-azure-metadata";

describe("<EntityAzureMetadata>", () => {
  it("renders queue-specific fields", () => {
    render(
      <EntityAzureMetadata
        entity={{
          entityType: "Queue",
          azureSourced: {
            status: "Active",
            azureResourceId: "/subscriptions/x/queues/orders-inbox",
            lockDuration: "PT1M",
            maxDeliveryCount: 10,
            duplicateDetection: { enabled: true, historyTimeWindow: "PT10M" },
            deadLettering: { deadLetterOnMessageExpiration: true },
            partitioning: { enabled: false },
            session: { enabled: false },
            forwarding: { forwardTo: null, forwardDeadLetteredMessagesTo: null },
            defaultTimeToLive: "P14D",
            maxSizeInMegabytes: 5120,
          },
        }}
      />,
    );
    expect(screen.getByTestId("azure-status")).toHaveTextContent("Active");
    expect(screen.getByTestId("azure-lock-duration")).toHaveTextContent("PT1M");
    expect(screen.getByTestId("azure-max-delivery-count")).toHaveTextContent("10");
    expect(screen.getByTestId("azure-duplicate-detection")).toHaveTextContent("Enabled (PT10M)");
    expect(screen.getByTestId("azure-dead-letter")).toHaveTextContent("Enabled");
    expect(screen.getByTestId("azure-partitioning")).toHaveTextContent("Disabled");
    expect(screen.getByTestId("azure-session")).toHaveTextContent("Disabled");
    expect(screen.getByTestId("azure-forward-to")).toHaveTextContent("Unknown");
    expect(screen.getByTestId("azure-max-size")).toHaveTextContent("5120");
  });

  it("renders subscription-specific fields", () => {
    render(
      <EntityAzureMetadata
        entity={{
          entityType: "Subscription",
          azureSourced: {
            status: "Active",
            azureResourceId: "/subscriptions/x/subscriptions/sub-1",
            lockDuration: "PT30S",
            maxDeliveryCount: 5,
            deadLettering: { deadLetterOnMessageExpiration: false },
            session: { enabled: true },
            forwarding: { forwardTo: "queues/destination" },
            defaultTimeToLive: "P1D",
          },
        }}
      />,
    );
    expect(screen.getByTestId("azure-lock-duration")).toHaveTextContent("PT30S");
    expect(screen.getByTestId("azure-session")).toHaveTextContent("Enabled");
    expect(screen.getByTestId("azure-forward-to")).toHaveTextContent("queues/destination");
  });

  it("renders the Rule edge case as Unknown for missing filter + action", () => {
    render(
      <EntityAzureMetadata
        entity={{
          entityType: "Rule",
          azureSourced: {
            status: "Active",
            azureResourceId: "/subscriptions/x/rules/r-1",
            filterType: "Sql",
          },
        }}
      />,
    );
    expect(screen.getByTestId("azure-filter-type")).toHaveTextContent("Sql");
    expect(screen.getByTestId("azure-filter-expression")).toHaveTextContent("Unknown");
    expect(screen.getByTestId("azure-action-expression")).toHaveTextContent("Unknown");
  });

  it("renders topic-specific fields without queue-only fields", () => {
    render(
      <EntityAzureMetadata
        entity={{
          entityType: "Topic",
          azureSourced: {
            status: "Active",
            azureResourceId: "/subscriptions/x/topics/t-1",
            duplicateDetection: { enabled: false, historyTimeWindow: null },
            partitioning: { enabled: true },
            defaultTimeToLive: "P14D",
            maxSizeInMegabytes: 1024,
          },
        }}
      />,
    );
    expect(screen.getByTestId("azure-duplicate-detection")).toHaveTextContent("Disabled");
    expect(screen.getByTestId("azure-partitioning")).toHaveTextContent("Enabled");
    expect(screen.queryByTestId("azure-lock-duration")).toBeNull();
  });

  it("marks the card with the entity type for downstream styling", () => {
    render(
      <EntityAzureMetadata
        entity={{
          entityType: "Queue",
          azureSourced: { status: "Active", azureResourceId: "/x" },
        }}
      />,
    );
    expect(screen.getByTestId("entity-azure-metadata")).toHaveAttribute("data-entity-type", "Queue");
  });
});
