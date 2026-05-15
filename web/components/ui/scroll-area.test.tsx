import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { ScrollArea } from "./scroll-area";

describe("ScrollArea", () => {
  it("is axe-clean", async () => {
    const { container } = render(
      <ScrollArea className="h-32 w-40">
        <div>Content</div>
      </ScrollArea>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
