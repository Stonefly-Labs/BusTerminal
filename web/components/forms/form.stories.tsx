import type { Meta, StoryObj } from "@storybook/nextjs";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

import { Field } from "./field";
import { Form } from "./form";
import { requiredString } from "@/lib/validation/zod-helpers";

const meta: Meta<typeof Form> = {
  title: "Forms/Form (composite)",
  component: Form,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Form>;

const schema = z.object({
  namespaceName: requiredString({ min: 3, max: 50 }),
  description: z.string().optional(),
});

export const Default: Story = {
  render: () => (
    <Form
      schema={schema}
      defaultValues={{ namespaceName: "", description: "" }}
      accessibleNameKey="form.submit.default"
      onSubmit={async (values) => {
        await new Promise((resolve) => setTimeout(resolve, 300));
        console.log(values);
      }}
    >
      {(form) => (
        <div className="flex w-80 flex-col gap-3">
          <Field control={form.control} name="namespaceName" labelKey="domain.namespace.label" required>
            <Input placeholder="bt-prod-westus" />
          </Field>
          <Field control={form.control} name="description" labelKey="form.submit.default">
            <Input placeholder="Optional description" />
          </Field>
          <Button type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? "Saving…" : "Save"}
          </Button>
        </div>
      )}
    </Form>
  ),
};
