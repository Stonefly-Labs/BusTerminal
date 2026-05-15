import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Alert, AlertDescription, AlertTitle } from "./alert";

describe("Alert", () => {
  it("is axe-clean across intents", async () => {
    const { container } = render(
      <div>
        {(["info", "success", "warning", "error"] as const).map((intent) => (
          <Alert key={intent} intent={intent}>
            <AlertTitle>Title</AlertTitle>
            <AlertDescription>Body</AlertDescription>
          </Alert>
        ))}
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
