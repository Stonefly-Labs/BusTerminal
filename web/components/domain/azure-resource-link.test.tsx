import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { AzureResourceLink } from "./azure-resource-link";

const SAMPLE_ID =
  "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-orders/providers/Microsoft.ServiceBus/namespaces/orders-westus";
const PORTAL_URL = `https://portal.azure.com/#@/resource${SAMPLE_ID}`;

describe("AzureResourceLink", () => {
  it("is axe-clean", async () => {
    const { container } = render(
      <AzureResourceLink resourceId={SAMPLE_ID} portalUrl={PORTAL_URL} label="orders-westus" />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("opens the portal URL in a new tab with safe rel attributes", () => {
    render(<AzureResourceLink resourceId={SAMPLE_ID} portalUrl={PORTAL_URL} />);
    const portalLink = screen.getByRole("link", { name: /open in azure portal/i });
    expect(portalLink).toHaveAttribute("href", PORTAL_URL);
    expect(portalLink).toHaveAttribute("target", "_blank");
    expect(portalLink).toHaveAttribute("rel", "noopener noreferrer");
  });
});
