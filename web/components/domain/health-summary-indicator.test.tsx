import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { HealthSummaryIndicator } from "./health-summary-indicator";

describe("HealthSummaryIndicator", () => {
  it("is axe-clean across roll-up states", async () => {
    const { container } = render(
      <div>
        <HealthSummaryIndicator counts={{ healthy: 24, degraded: 0, unhealthy: 0 }} />
        <HealthSummaryIndicator counts={{ healthy: 18, degraded: 3, unhealthy: 0 }} />
        <HealthSummaryIndicator counts={{ healthy: 14, degraded: 2, unhealthy: 1 }} />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
