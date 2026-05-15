import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

import { Field } from "./field";
import { Form } from "./form";
import { requiredString } from "@/lib/validation/zod-helpers";

const schema = z.object({ name: requiredString() });

describe("Form composite", () => {
  it("is axe-clean", async () => {
    const { container } = render(
      <Form
        schema={schema}
        accessibleNameKey="form.submit.default"
        onSubmit={() => undefined}
        defaultValues={{ name: "" }}
      >
        {(form) => (
          <div>
            <Field control={form.control} name="name" labelKey="domain.namespace.label" required>
              <Input />
            </Field>
            <Button type="submit">Save</Button>
          </div>
        )}
      </Form>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
