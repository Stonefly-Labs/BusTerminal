import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Command, CommandInput, CommandList } from "./command";

describe("Command", () => {
  it("is axe-clean", async () => {
    const { container } = render(
      <Command label="Test command palette">
        <CommandInput aria-label="Search" />
        <CommandList />
      </Command>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
