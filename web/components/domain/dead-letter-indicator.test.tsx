import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { DeadLetterIndicator } from "./dead-letter-indicator";

describe("DeadLetterIndicator", () => {
  it("is axe-clean at zero and positive counts", async () => {
    const { container } = render(
      <div>
        <DeadLetterIndicator count={0} />
        <DeadLetterIndicator count={14} />
        <DeadLetterIndicator count={2_400} size="sm" />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
