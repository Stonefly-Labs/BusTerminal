import type { Meta, StoryObj } from "@storybook/nextjs";

import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from "./select";
import { Label } from "./label";

const meta: Meta<typeof Select> = {
  title: "Primitives/Select",
  component: Select,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Select>;

export const Default: Story = {
  render: () => (
    <div className="flex w-72 flex-col gap-2">
      <Label htmlFor="env-select">Environment</Label>
      <Select>
        <SelectTrigger id="env-select">
          <SelectValue placeholder="Pick an environment" />
        </SelectTrigger>
        <SelectContent>
          <SelectGroup>
            <SelectLabel>Lower</SelectLabel>
            <SelectItem value="dev">Development</SelectItem>
            <SelectItem value="test">Test</SelectItem>
          </SelectGroup>
          <SelectSeparator />
          <SelectGroup>
            <SelectLabel>Upper</SelectLabel>
            <SelectItem value="staging">Staging</SelectItem>
            <SelectItem value="prod">Production</SelectItem>
          </SelectGroup>
        </SelectContent>
      </Select>
    </div>
  ),
};
