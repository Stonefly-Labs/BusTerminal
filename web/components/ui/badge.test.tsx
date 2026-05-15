import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Badge } from "./badge";

describe("Badge", () => {
  it("is axe-clean across intents", async () => {
    const { container } = render(
      <div>
        <Badge>Neutral</Badge>
        <Badge intent="accent">Accent</Badge>
        <Badge intent="success">Success</Badge>
        <Badge intent="warning">Warning</Badge>
        <Badge intent="error">Error</Badge>
        <Badge intent="info">Info</Badge>
        <Badge intent="outline">Outline</Badge>
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
