import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "./tooltip";
import { Button } from "./button";

describe("Tooltip", () => {
  it("is axe-clean (closed)", async () => {
    const { container } = render(
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button aria-label="Help">?</Button>
          </TooltipTrigger>
          <TooltipContent>Body</TooltipContent>
        </Tooltip>
      </TooltipProvider>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
