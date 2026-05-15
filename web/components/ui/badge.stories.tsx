import type { Meta, StoryObj } from "@storybook/nextjs";
import { Hash } from "lucide-react";

import { Badge } from "./badge";

const meta: Meta<typeof Badge> = {
  title: "Primitives/Badge",
  component: Badge,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Badge>;

/**
 * Semantic intents auto-render the canonical icon for their state so meaning
 * is conveyed through color + icon + text (FR-026 / T103). Neutral / accent /
 * outline intents stay icon-less by default.
 */
export const AllIntents: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <Badge intent="neutral">Neutral</Badge>
      <Badge intent="accent">Accent</Badge>
      <Badge intent="success">Success</Badge>
      <Badge intent="warning">Warning</Badge>
      <Badge intent="error">Error</Badge>
      <Badge intent="info">Info</Badge>
      <Badge intent="outline">Outline</Badge>
    </div>
  ),
};

/**
 * Demonstrates the icon overrides — pass a Lucide component for non-semantic
 * affordances (e.g., an ID badge) or `false` to suppress the auto-icon on
 * semantic intents.
 */
export const IconOverrides: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-2">
      <Badge intent="neutral" icon={Hash}>
        order-481
      </Badge>
      <Badge intent="success" icon={false}>
        No icon
      </Badge>
      <Badge intent="info">Info default</Badge>
    </div>
  ),
};

/**
 * Side-by-side parity with Alert / InlineValidation / Toast — every semantic
 * surface uses the same color + icon mapping so operators can scan a screen
 * and recognize state from any primitive (FR-026).
 */
export const SemanticParity: Story = {
  render: () => (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs uppercase text-foreground-muted">Active</span>
        <Badge intent="success">Healthy</Badge>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs uppercase text-foreground-muted">Quota</span>
        <Badge intent="warning">84% used</Badge>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs uppercase text-foreground-muted">Dead-letter</span>
        <Badge intent="error">14 messages</Badge>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs uppercase text-foreground-muted">Job</span>
        <Badge intent="info">Discovery running</Badge>
      </div>
    </div>
  ),
};
