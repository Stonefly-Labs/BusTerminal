/**
 * Shared shape for Queue domain composites (QueueRow + QueueCard).
 */

export type QueueStatus = "active" | "idle" | "error" | "dead-lettered";

export interface QueueSummary {
  readonly id: string;
  readonly name: string;
  readonly status: QueueStatus;
  readonly activeCount: number;
  readonly deadLetterCount: number;
  readonly maxDelivery?: number;
}

export const QUEUE_STATUS_KEY = {
  active: "domain.entity.status.active",
  idle: "domain.entity.status.idle",
  error: "domain.entity.status.error",
  "dead-lettered": "domain.entity.status.deadLettered",
} as const satisfies Record<
  QueueStatus,
  | "domain.entity.status.active"
  | "domain.entity.status.idle"
  | "domain.entity.status.error"
  | "domain.entity.status.deadLettered"
>;

export const QUEUE_STATUS_INTENT = {
  active: "success",
  idle: "neutral",
  error: "error",
  "dead-lettered": "warning",
} as const satisfies Record<QueueStatus, "success" | "neutral" | "error" | "warning">;
