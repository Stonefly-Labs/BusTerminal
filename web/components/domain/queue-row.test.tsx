import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { QueueRow } from "./queue-row";
import type { QueueSummary } from "./queue-types";

const BASE: QueueSummary = {
  id: "q-001",
  name: "orders.in",
  status: "active",
  activeCount: 1240,
  deadLetterCount: 0,
};

const LONG_NAME =
  "operations-platform-message-routing-prod-westus3-orders-replay-canary-shadow-extra-tail";

describe("QueueRow", () => {
  it("is axe-clean across statuses", async () => {
    const { container } = render(
      <div>
        <QueueRow queue={BASE} />
        <QueueRow queue={{ ...BASE, id: "q-2", status: "idle" }} />
        <QueueRow queue={{ ...BASE, id: "q-3", status: "error", deadLetterCount: 14 }} />
        <QueueRow queue={{ ...BASE, id: "q-4", status: "dead-lettered", deadLetterCount: 3 }} />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("truncates oversized queue names and exposes the full value to assistive tech", () => {
    render(<QueueRow queue={{ ...BASE, name: LONG_NAME }} />);
    const trigger = screen.getByTestId("truncated-name-trigger");
    expect(trigger).toHaveTextContent(LONG_NAME);
    expect(trigger).toHaveClass("truncate");
    expect(trigger).toHaveAttribute("tabindex", "0");
    expect(trigger).toHaveAttribute("aria-label", LONG_NAME);
  });
});
