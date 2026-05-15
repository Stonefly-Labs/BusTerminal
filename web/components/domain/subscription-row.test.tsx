import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { SubscriptionRow } from "./subscription-row";
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

describe("SubscriptionRow", () => {
  it("is axe-clean across statuses", async () => {
    const { container } = render(
      <div>
        <SubscriptionRow subscription={BASE} />
        <SubscriptionRow subscription={{ ...BASE, id: "s-2", status: "idle" }} />
        <SubscriptionRow subscription={{ ...BASE, id: "s-3", status: "error" }} />
        <SubscriptionRow
          subscription={{ ...BASE, id: "s-4", status: "dead-lettered", deadLetterCount: 3 }}
        />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("truncates oversized subscription names and exposes the full value to assistive tech", () => {
    render(<SubscriptionRow subscription={{ ...BASE, name: LONG_NAME }} />);
    const trigger = screen.getByTestId("truncated-name-trigger");
    expect(trigger).toHaveTextContent(LONG_NAME);
    expect(trigger).toHaveClass("truncate");
    expect(trigger).toHaveAttribute("tabindex", "0");
    expect(trigger).toHaveAttribute("aria-label", LONG_NAME);
  });
});
