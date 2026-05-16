import type { Meta, StoryObj } from "@storybook/nextjs";
import { Info } from "lucide-react";

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "./tooltip";
import { Button } from "./button";

const meta: Meta<typeof Tooltip> = {
  title: "Primitives/Tooltip",
  component: Tooltip,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Tooltip>;

export const Default: Story = {
  render: () => (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button intent="ghost" size="icon" aria-label="Info">
            <Info />
          </Button>
        </TooltipTrigger>
        <TooltipContent>Surface partition info</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  ),
};
