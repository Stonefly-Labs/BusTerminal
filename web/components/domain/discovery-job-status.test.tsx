import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { DiscoveryJobStatus } from "./discovery-job-status";

const NOW = new Date("2026-05-15T12:00:00Z");
const FIVE_MIN_AGO = new Date(NOW.getTime() - 5 * 60_000);

describe("DiscoveryJobStatus", () => {
  it("is axe-clean across job states", async () => {
    const { container } = render(
      <div>
        <DiscoveryJobStatus state="queued" now={NOW} />
        <DiscoveryJobStatus state="running" startedAt={FIVE_MIN_AGO} now={NOW} />
        <DiscoveryJobStatus state="succeeded" startedAt={FIVE_MIN_AGO} now={NOW} />
        <DiscoveryJobStatus state="failed" startedAt={FIVE_MIN_AGO} now={NOW} />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
