import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Button } from "./button";

describe("Button", () => {
  it("renders with the provided label", () => {
    render(<Button>Submit</Button>);
    expect(screen.getByRole("button", { name: "Submit" })).toBeInTheDocument();
  });

  it("respects the disabled attribute", () => {
    render(<Button disabled>Submit</Button>);
    expect(screen.getByRole("button")).toBeDisabled();
  });

  it("has no axe violations across intents", async () => {
    const { container } = render(
      <div>
        <Button>Primary</Button>
        <Button intent="secondary">Secondary</Button>
        <Button intent="outline">Outline</Button>
        <Button intent="ghost">Ghost</Button>
        <Button intent="destructive">Destructive</Button>
        <Button intent="link">Link</Button>
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
