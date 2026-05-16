import { z } from "zod";

import { requiredString } from "@/lib/validation/zod-helpers";

export const newQueueSchema = z.object({
  name: requiredString({ min: 3, max: 50 }),
  maxDelivery: z.coerce.number().int().min(1).max(100),
  description: z.string().max(280).optional().or(z.literal("")),
});

export type NewQueueValues = z.infer<typeof newQueueSchema>;
