import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { ContextMenu, ContextMenuTrigger } from "./context-menu";

describe("ContextMenu", () => {
  it("is axe-clean (closed)", async () => {
    const { container } = render(
      <ContextMenu>
        <ContextMenuTrigger>
          <span>Trigger</span>
        </ContextMenuTrigger>
      </ContextMenu>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
