import type { Meta, StoryObj } from "@storybook/nextjs";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "./tabs";

const meta: Meta<typeof Tabs> = {
  title: "Primitives/Tabs",
  component: Tabs,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Tabs>;

export const Default: Story = {
  render: () => (
    <Tabs defaultValue="overview" className="w-96">
      <TabsList>
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="rules">Rules</TabsTrigger>
        <TabsTrigger value="metrics">Metrics</TabsTrigger>
      </TabsList>
      <TabsContent value="overview" className="text-sm">
        Overview content
      </TabsContent>
      <TabsContent value="rules" className="text-sm">
        Rules content
      </TabsContent>
      <TabsContent value="metrics" className="text-sm">
        Metrics content
      </TabsContent>
    </Tabs>
  ),
};
