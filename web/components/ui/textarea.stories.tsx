import type { Meta, StoryObj } from "@storybook/nextjs";

import { Textarea } from "./textarea";
import { Label } from "./label";

const meta: Meta<typeof Textarea> = {
  title: "Primitives/Textarea",
  component: Textarea,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Textarea>;

export const Default: Story = {
  render: () => (
    <div className="flex w-96 flex-col gap-2">
      <Label htmlFor="rule-filter">Subscription rule SQL filter</Label>
      <Textarea id="rule-filter" placeholder="user.region = 'westus'" />
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div className="flex w-96 flex-col gap-4">
      <Textarea aria-label="Idle textarea" placeholder="Idle" />
      <Textarea aria-label="Disabled textarea" placeholder="Disabled" disabled />
      <Textarea
        aria-label="Invalid textarea"
        aria-invalid="true"
        defaultValue="invalid expression"
      />
    </div>
  ),
};

/**
 * `mono` renders the textarea content with the monospace family — for JSON
 * payloads, SQL filters, structured message metadata, connection strings
 * (FR-009 / T102).
 */
export const Monospace: Story = {
  render: () => (
    <div className="flex w-[28rem] flex-col gap-4">
      <div className="flex flex-col gap-2">
        <Label htmlFor="json-payload">Message payload</Label>
        <Textarea
          id="json-payload"
          mono
          rows={8}
          defaultValue={`{
  "orderId": "01H8XK4Y5Z9M2N6P3Q1R8T7V0W",
  "customer": "acme-corp",
  "amount": 124.5,
  "lineItems": 3
}`}
        />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="sql-filter">Subscription rule SQL filter</Label>
        <Textarea
          id="sql-filter"
          mono
          rows={3}
          defaultValue="user.region = 'westus' AND priority > 5"
        />
      </div>
    </div>
  ),
};
