import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { EntityRelationshipBadge } from "./entity-relationship-badge";

describe("EntityRelationshipBadge", () => {
  it("is axe-clean across relationship kinds", async () => {
    const { container } = render(
      <div>
        <EntityRelationshipBadge kind="forwards" from="orders.in" to="orders.archive" />
        <EntityRelationshipBadge kind="subscribes" from="billing" to="orders.events" />
        <EntityRelationshipBadge kind="deadLetters" from="orders.in" to="orders.dlq" />
        <EntityRelationshipBadge kind="publishes" from="orders-api" to="orders.events" />
        <EntityRelationshipBadge kind="parentOf" from="orders-westus" to="orders.in" />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
