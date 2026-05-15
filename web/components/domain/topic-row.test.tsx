import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { TopicRow } from "./topic-row";
import type { TopicSummary } from "./topic-types";

const BASE: TopicSummary = {
  id: "t-001",
  name: "orders.events",
  status: "active",
  subscriptionCount: 4,
  messageCount: 18_420,
};

const LONG_NAME =
  "operations-platform-event-routing-prod-westus3-orders-domain-events-shadow-extra-tail";

describe("TopicRow", () => {
  it("is axe-clean across statuses", async () => {
    const { container } = render(
      <div>
        <TopicRow topic={BASE} />
        <TopicRow topic={{ ...BASE, id: "t-2", status: "idle" }} />
        <TopicRow topic={{ ...BASE, id: "t-3", status: "error" }} />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("truncates oversized topic names and exposes the full value to assistive tech", () => {
    render(<TopicRow topic={{ ...BASE, name: LONG_NAME }} />);
    const trigger = screen.getByTestId("truncated-name-trigger");
    expect(trigger).toHaveTextContent(LONG_NAME);
    expect(trigger).toHaveClass("truncate");
    expect(trigger).toHaveAttribute("tabindex", "0");
    expect(trigger).toHaveAttribute("aria-label", LONG_NAME);
  });
});
