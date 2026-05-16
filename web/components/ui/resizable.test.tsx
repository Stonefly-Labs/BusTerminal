import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "./resizable";

describe("Resizable", () => {
  it("renders the resize handle as a focusable separator and stays axe-clean", async () => {
    const { container, getByRole } = render(
      <div className="h-32 w-64">
        <ResizablePanelGroup direction="horizontal">
          <ResizablePanel defaultSize={50}>
            <div>A</div>
          </ResizablePanel>
          <ResizableHandle />
          <ResizablePanel defaultSize={50}>
            <div>B</div>
          </ResizablePanel>
        </ResizablePanelGroup>
      </div>,
    );
    expect(getByRole("separator")).toHaveAttribute("tabindex");
    // axe runs with `aria-required-attr` disabled because react-resizable-panels
    // sets aria-valuenow only after layout, and jsdom never runs layout. The
    // rule is exercised end-to-end by the Playwright a11y suite.
    const results = await axe(container, {
      rules: { "aria-required-attr": { enabled: false } },
    });
    expect(results).toHaveNoViolations();
  });
});
