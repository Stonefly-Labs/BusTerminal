import type { Meta, StoryObj } from "@storybook/nextjs";

import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from "./command";

const meta: Meta<typeof Command> = {
  title: "Primitives/Command",
  component: Command,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Command>;

export const Default: Story = {
  render: () => (
    <Command className="w-96 rounded-md border border-border-default">
      <CommandInput />
      <CommandList>
        <CommandEmpty>No results.</CommandEmpty>
        <CommandGroup heading="Suggestions">
          <CommandItem>Search namespaces</CommandItem>
          <CommandItem>Search queues</CommandItem>
          <CommandItem>
            Open dashboard <CommandShortcut>⌘D</CommandShortcut>
          </CommandItem>
        </CommandGroup>
        <CommandSeparator />
        <CommandGroup heading="Actions">
          <CommandItem>Switch theme</CommandItem>
          <CommandItem>Sign out</CommandItem>
        </CommandGroup>
      </CommandList>
    </Command>
  ),
};
