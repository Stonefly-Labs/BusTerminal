/**
 * Spec 009 / T067 / US2. Component test for `<EntityDiscoveryInfo>`.
 * Covers: lifecycle badge mapping, first/last-seen rendering, and last
 * discovery run id surfacing.
 */

import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import { EntityDiscoveryInfo } from "./entity-discovery-info";

describe("<EntityDiscoveryInfo>", () => {
  it("renders the Active lifecycle badge", () => {
    render(
      <EntityDiscoveryInfo
        entity={{
          lifecycleStatus: "Active",
          firstDiscoveredUtc: "2026-06-15T10:00:00Z",
          lastSeenUtc: "2026-06-17T14:32:00Z",
          lastDiscoveryRunId: "dr_TEST00000000000000000000001",
        }}
      />,
    );
    const badge = screen.getByTestId("entity-lifecycle-badge");
    expect(badge).toHaveTextContent("Active");
    expect(badge).toHaveAttribute("aria-label", "Lifecycle: Active");
  });

  it("renders the Missing lifecycle badge with the warning intent label", () => {
    render(
      <EntityDiscoveryInfo
        entity={{
          lifecycleStatus: "Missing",
          firstDiscoveredUtc: "2026-06-15T10:00:00Z",
          lastSeenUtc: "2026-06-17T14:32:00Z",
          lastDiscoveryRunId: "dr_2",
        }}
      />,
    );
    expect(screen.getByTestId("entity-lifecycle-badge")).toHaveTextContent("Missing");
  });

  it("renders the first discovered and last seen timestamps", () => {
    render(
      <EntityDiscoveryInfo
        entity={{
          lifecycleStatus: "Active",
          firstDiscoveredUtc: "2026-06-15T10:00:00Z",
          lastSeenUtc: "2026-06-17T14:32:00Z",
          lastDiscoveryRunId: "dr_TEST",
        }}
      />,
    );
    expect(screen.getByTestId("entity-first-discovered")).toHaveAttribute(
      "datetime",
      "2026-06-15T10:00:00Z",
    );
    expect(screen.getByTestId("entity-last-seen")).toHaveAttribute(
      "datetime",
      "2026-06-17T14:32:00Z",
    );
  });

  it("renders the last discovery run id when present", () => {
    render(
      <EntityDiscoveryInfo
        entity={{
          lifecycleStatus: "Active",
          firstDiscoveredUtc: "2026-06-15T10:00:00Z",
          lastSeenUtc: "2026-06-17T14:32:00Z",
          lastDiscoveryRunId: "dr_VISIBLE",
        }}
      />,
    );
    expect(screen.getByTestId("entity-last-run-id")).toHaveTextContent("dr_VISIBLE");
  });

  it("omits the run id row when no last discovery run id is provided", () => {
    render(
      <EntityDiscoveryInfo
        entity={{
          lifecycleStatus: "Archived",
          firstDiscoveredUtc: "2026-06-15T10:00:00Z",
          lastSeenUtc: "2026-06-17T14:32:00Z",
          lastDiscoveryRunId: "",
        }}
      />,
    );
    expect(screen.queryByTestId("entity-last-run-id")).toBeNull();
  });
});
