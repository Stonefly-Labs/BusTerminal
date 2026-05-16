import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { MessageCountIndicator } from "./message-count-indicator";

describe("MessageCountIndicator", () => {
  it("is axe-clean across counts", async () => {
    const { container } = render(
      <div>
        <MessageCountIndicator count={0} />
        <MessageCountIndicator count={1_240} />
        <MessageCountIndicator count={1_240_000} />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
