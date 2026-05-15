import type { Meta, StoryObj } from "@storybook/nextjs";

import { Input } from "./input";
import { Label } from "./label";

const meta: Meta<typeof Input> = {
  title: "Primitives/Input",
  component: Input,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Input>;

export const Default: Story = {
  render: () => (
    <div className="flex w-80 flex-col gap-2">
      <Label htmlFor="default-input">Namespace name</Label>
      <Input id="default-input" placeholder="bt-prod-westus" />
    </div>
  ),
};

export const Variants: Story = {
  render: () => (
    <div className="flex w-80 flex-col gap-4">
      <div className="flex flex-col gap-2">
        <Label htmlFor="text-input">Text</Label>
        <Input id="text-input" defaultValue="bt-prod-westus" />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="number-input">Max delivery count</Label>
        <Input id="number-input" type="number" defaultValue={10} />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="password-input">Connection token</Label>
        <Input id="password-input" type="password" defaultValue="redacted" />
      </div>
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div className="flex w-80 flex-col gap-4">
      <Input placeholder="Idle" />
      <Input placeholder="Disabled" disabled />
      <Input placeholder="Invalid" aria-invalid="true" defaultValue="not allowed" />
    </div>
  ),
};

/**
 * `mono` switches the input to the monospace family for technical identifiers
 * — queue / topic / subscription / namespace names, correlation IDs,
 * connection strings (FR-009 / T102).
 */
export const Monospace: Story = {
  render: () => (
    <div className="flex w-96 flex-col gap-4">
      <div className="flex flex-col gap-2">
        <Label htmlFor="queue-name">Queue name</Label>
        <Input
          id="queue-name"
          mono
          defaultValue="orders.westus.priority"
          aria-describedby="queue-name-hint"
        />
        <span id="queue-name-hint" className="text-xs text-foreground-muted">
          Monospace renders queue names with unambiguous character widths.
        </span>
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="correlation-id">Correlation ID</Label>
        <Input
          id="correlation-id"
          mono
          defaultValue="01H8XK4Y5Z9M2N6P3Q1R8T7V0W"
        />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="connection-string">Connection string</Label>
        <Input
          id="connection-string"
          mono
          defaultValue="Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=…"
        />
      </div>
    </div>
  ),
};
