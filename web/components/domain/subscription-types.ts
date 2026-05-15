/**
 * Shared shape for Subscription domain composites (SubscriptionRow + SubscriptionCard).
 */

export type SubscriptionStatus = "active" | "idle" | "error" | "dead-lettered";

export interface SubscriptionSummary {
  readonly id: string;
  readonly name: string;
  readonly parentTopic: string;
  readonly status: SubscriptionStatus;
  readonly messageCount: number;
  readonly deadLetterCount: number;
}

export const SUBSCRIPTION_STATUS_KEY = {
  active: "domain.entity.status.active",
  idle: "domain.entity.status.idle",
  error: "domain.entity.status.error",
  "dead-lettered": "domain.entity.status.deadLettered",
} as const satisfies Record<
  SubscriptionStatus,
  | "domain.entity.status.active"
  | "domain.entity.status.idle"
  | "domain.entity.status.error"
  | "domain.entity.status.deadLettered"
>;

export const SUBSCRIPTION_STATUS_INTENT = {
  active: "success",
  idle: "neutral",
  error: "error",
  "dead-lettered": "warning",
} as const satisfies Record<SubscriptionStatus, "success" | "neutral" | "error" | "warning">;
