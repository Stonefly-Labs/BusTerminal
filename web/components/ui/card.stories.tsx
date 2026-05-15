import type { Meta, StoryObj } from "@storybook/nextjs";

import { Button } from "./button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "./card";

const meta: Meta<typeof Card> = {
  title: "Primitives/Card",
  component: Card,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Card>;

export const Default: Story = {
  render: () => (
    <Card className="w-96">
      <CardHeader>
        <CardTitle>orders-westus</CardTitle>
        <CardDescription>Premium namespace · 2 queues · 3 topics</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-foreground-muted">
          Last messaging activity 4 minutes ago. No dead-lettered messages.
        </p>
      </CardContent>
      <CardFooter>
        <Button intent="secondary" size="sm">Open</Button>
        <Button intent="ghost" size="sm">Inspect</Button>
      </CardFooter>
    </Card>
  ),
};
