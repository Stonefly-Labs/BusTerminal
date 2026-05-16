import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { MetadataKeyValuePanel } from "./metadata-key-value-panel";

describe("MetadataKeyValuePanel", () => {
  it("is axe-clean when populated", async () => {
    const { container } = render(
      <MetadataKeyValuePanel
        entries={[
          { key: "Namespace", value: "orders-westus", mono: false },
          { key: "Owner", value: "ops-team@busterminal.dev" },
        ]}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("renders the documented empty copy when no entries are provided", () => {
    render(<MetadataKeyValuePanel entries={[]} />);
    expect(screen.getByText(/no metadata recorded/i)).toBeInTheDocument();
  });

  it("uses semantic definition-list markup", () => {
    const { container } = render(
      <MetadataKeyValuePanel
        entries={[{ key: "Namespace", value: "orders-westus", mono: false }]}
      />,
    );
    expect(container.querySelector("dl")).not.toBeNull();
    expect(container.querySelector("dt")).not.toBeNull();
    expect(container.querySelector("dd")).not.toBeNull();
  });
});
