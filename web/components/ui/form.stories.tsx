import type { Meta, StoryObj } from "@storybook/nextjs";
import { useForm } from "react-hook-form";

import { Button } from "./button";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "./form";
import { Input } from "./input";

const meta: Meta<typeof Form> = {
  title: "Primitives/Form (low-level)",
  component: Form,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Form>;

interface Values {
  readonly namespaceName: string;
}

function Demo() {
  const form = useForm<Values>({ defaultValues: { namespaceName: "" } });
  return (
    <Form {...form}>
      <form className="flex w-80 flex-col gap-4" onSubmit={form.handleSubmit(() => undefined)}>
        <FormField
          control={form.control}
          name="namespaceName"
          rules={{ required: "Required" }}
          render={({ field }) => (
            <FormItem>
              <FormLabel>Namespace name</FormLabel>
              <FormControl>
                <Input placeholder="bt-prod-westus" {...field} />
              </FormControl>
              <FormDescription>Lowercase, kebab-case identifier.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit">Save</Button>
      </form>
    </Form>
  );
}

export const Default: Story = {
  render: () => <Demo />,
};
