import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Skeleton } from "./skeleton";

describe("Skeleton", () => {
  it("is axe-clean", async () => {
    const { container } = render(<Skeleton className="h-6 w-32" />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
