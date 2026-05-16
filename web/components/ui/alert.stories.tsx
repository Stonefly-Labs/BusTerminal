import type { Meta, StoryObj } from "@storybook/nextjs";

import { Alert, AlertDescription, AlertTitle } from "./alert";

const meta: Meta<typeof Alert> = {
  title: "Primitives/Alert",
  component: Alert,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Alert>;

export const AllIntents: Story = {
  render: () => (
    <div className="flex w-96 flex-col gap-3">
      <Alert intent="info">
        <AlertTitle>New discovery job available</AlertTitle>
        <AlertDescription>
          Run a discovery sweep against the orders-westus namespace.
        </AlertDescription>
      </Alert>
      <Alert intent="success">
        <AlertTitle>Save complete</AlertTitle>
        <AlertDescription>The subscription rule has been published.</AlertDescription>
      </Alert>
      <Alert intent="warning">
        <AlertTitle>Approaching message quota</AlertTitle>
        <AlertDescription>orders-westus is at 84% of premium quota.</AlertDescription>
      </Alert>
      <Alert intent="error">
        <AlertTitle>Authorization failed</AlertTitle>
        <AlertDescription>
          Managed identity could not access the namespace. Re-check role assignments.
        </AlertDescription>
      </Alert>
    </div>
  ),
};
