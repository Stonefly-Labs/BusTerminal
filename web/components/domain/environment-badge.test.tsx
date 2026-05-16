import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { EnvironmentBadge } from "./environment-badge";

describe("EnvironmentBadge", () => {
  it("is axe-clean across environments", async () => {
    const { container } = render(
      <div>
        <EnvironmentBadge environment="dev" />
        <EnvironmentBadge environment="test" />
        <EnvironmentBadge environment="staging" />
        <EnvironmentBadge environment="prod" />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("announces the environment explicitly to assistive tech", () => {
    const { getByRole } = render(<EnvironmentBadge environment="prod" />);
    expect(getByRole("status")).toHaveAttribute("aria-label", "Environment: Production");
  });
});
