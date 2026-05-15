import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { SubscriptionCard } from "./subscription-card";
import type { SubscriptionSummary } from "./subscription-types";

const BASE: SubscriptionSummary = {
  id: "s-001",
  name: "billing-pipeline",
  parentTopic: "orders.events",
  status: "active",
  messageCount: 8_120,
  deadLetterCount: 0,
};

const LONG_NAME =
  "operations-platform-billing-pipeline-prod-westus3-orders-events-shadow-extra-tail";

describe("SubscriptionCard", () => {
  it("is axe-clean across statuses", async () => {
    const { container } = render(
      <div>
        <SubscriptionCard subscription={BASE} />
        <SubscriptionCard subscription={{ ...BASE, id: "s-2", status: "idle" }} />
        <SubscriptionCard subscription={{ ...BASE, id: "s-3", status: "error" }} />
        <SubscriptionCard
          subscription={{ ...BASE, id: "s-4", status: "dead-lettered", deadLetterCount: 3 }}
        />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("truncates oversized subscription names and exposes the full value to assistive tech", () => {
    render(<SubscriptionCard subscription={{ ...BASE, name: LONG_NAME }} />);
    const trigger = screen.getByTestId("truncated-name-trigger");
    expect(trigger).toHaveTextContent(LONG_NAME);
    expect(trigger).toHaveClass("truncate");
    expect(trigger).toHaveAttribute("tabindex", "0");
    expect(trigger).toHaveAttribute("aria-label", LONG_NAME);
  });
});
