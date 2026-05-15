/**
 * Shared shape for Topic domain composites (TopicRow + TopicCard).
 */

export type TopicStatus = "active" | "idle" | "error";

export interface TopicSummary {
  readonly id: string;
  readonly name: string;
  readonly status: TopicStatus;
  readonly subscriptionCount: number;
  readonly messageCount: number;
}

export const TOPIC_STATUS_KEY = {
  active: "domain.entity.status.active",
  idle: "domain.entity.status.idle",
  error: "domain.entity.status.error",
} as const satisfies Record<
  TopicStatus,
  "domain.entity.status.active" | "domain.entity.status.idle" | "domain.entity.status.error"
>;

export const TOPIC_STATUS_INTENT = {
  active: "success",
  idle: "neutral",
  error: "error",
} as const satisfies Record<TopicStatus, "success" | "neutral" | "error">;
