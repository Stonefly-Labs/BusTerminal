import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { NamespaceCard, type NamespaceSummary } from "./namespace-card";

const BASE: NamespaceSummary = {
  id: "ns-001",
  name: "orders-westus",
  tier: "premium",
  region: "westus",
  status: "healthy",
  queueCount: 12,
  topicCount: 5,
};

const LONG_NAME =
  "operations-platform-message-routing-prod-westus3-ns-2026-rev-04-canary-shadow-extra-tail";

describe("NamespaceCard", () => {
  it("is axe-clean across statuses", async () => {
    const { container } = render(
      <div>
        <NamespaceCard namespace={BASE} />
        <NamespaceCard namespace={{ ...BASE, id: "ns-2", status: "degraded" }} />
        <NamespaceCard namespace={{ ...BASE, id: "ns-3", status: "unhealthy" }} />
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it("renders an oversized name with CSS-only truncation and surfaces the full value to assistive tech", () => {
    render(<NamespaceCard namespace={{ ...BASE, name: LONG_NAME }} />);
    const trigger = screen.getByTestId("truncated-name-trigger");
    expect(trigger).toHaveTextContent(LONG_NAME);
    expect(trigger).toHaveClass("truncate");
    expect(trigger).toHaveAttribute("tabindex", "0");
    expect(trigger).toHaveAttribute("aria-label", LONG_NAME);
  });
});
