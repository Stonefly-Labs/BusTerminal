import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Button } from "./button";
import { DropdownMenu, DropdownMenuTrigger } from "./dropdown-menu";

describe("DropdownMenu", () => {
  it("is axe-clean (closed)", async () => {
    const { container } = render(
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button>Open</Button>
        </DropdownMenuTrigger>
      </DropdownMenu>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
