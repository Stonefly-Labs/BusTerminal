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
  parameters: {
    layout: "centered",
    // cmdk wraps the listbox content in role-less `cmdk-list-sizer` and forces
    // `role="presentation"` on its Group wrapper. axe's `aria-required-children`
    // doesn't traverse that wrapper chain and reports the listbox as having no
    // option/group children — even though the rendered options carry
    // `role="option"` and the inner `cmdk-group-items` carries `role="group"`
    // with proper `aria-labelledby`. Keyboard nav and AT semantics are intact.
    // Tracked upstream: https://github.com/pacocoursey/cmdk/issues/280
    a11y: {
      config: {
        rules: [{ id: "aria-required-children", enabled: false }],
      },
    },
  },
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
