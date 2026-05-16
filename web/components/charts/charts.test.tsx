import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { ChartLine } from "./chart-line";

const DATA = [
  { x: "Mon", y: 1 },
  { x: "Tue", y: 4 },
  { x: "Wed", y: 2 },
];

describe("Charts", () => {
  it("ChartLine is axe-clean and exposes an accessible name", async () => {
    const { container, getByRole } = render(
      <div style={{ width: 320, height: 200 }}>
        <ChartLine
          data={DATA}
          xKey="x"
          series={[{ id: "y", accessor: "y", label: "Value" }]}
          accessibleLabel="Sample chart"
        />
      </div>,
    );
    expect(getByRole("img", { name: "Sample chart" })).toBeInTheDocument();
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
