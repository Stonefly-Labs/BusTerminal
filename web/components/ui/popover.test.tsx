import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Popover, PopoverContent, PopoverTrigger } from "./popover";
import { Button } from "./button";

describe("Popover", () => {
  it("is axe-clean (closed)", async () => {
    const { container } = render(
      <Popover>
        <PopoverTrigger asChild>
          <Button>Open</Button>
        </PopoverTrigger>
        <PopoverContent>Body</PopoverContent>
      </Popover>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
