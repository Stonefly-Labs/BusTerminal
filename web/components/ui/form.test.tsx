import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { useForm } from "react-hook-form";
import { axe } from "vitest-axe";

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

function Wrapper() {
  const form = useForm<{ a: string }>({ defaultValues: { a: "" } });
  return (
    <Form {...form}>
      <form>
        <FormField
          control={form.control}
          name="a"
          render={({ field }) => (
            <FormItem>
              <FormLabel>A</FormLabel>
              <FormControl>
                <Input {...field} />
              </FormControl>
              <FormDescription>Help</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
      </form>
    </Form>
  );
}

describe("Form (low-level)", () => {
  it("is axe-clean", async () => {
    const { container } = render(<Wrapper />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
