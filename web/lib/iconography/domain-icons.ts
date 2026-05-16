/**
 * BusTerminal domain iconography (FR-021 / FR-022).
 *
 * Curated mapping from Service Bus domain concepts to `lucide-react` icons.
 * The single-family rule (FR-021) means every domain icon in the foundation
 * resolves through this module — domain composites consume it, never raw
 * `lucide-react` imports.
 *
 * Consistency rules:
 *   - Default `strokeWidth` is `1.5` across the family.
 *   - The `accessibleLabelKey` is sourced from the i18n string surface so
 *     decorative use can omit it without violating FR-026 (state must be
 *     conveyed by icon + text, never icon alone).
 *
 * Research R9 motivates the curated rather than free-form approach.
 */

import {
  type LucideIcon,
  AlertOctagon,
  ArrowRightLeft,
  BellRing,
  Boxes,
  Cloud,
  Globe,
  Inbox,
  MailWarning,
  Megaphone,
  Network,
  Repeat,
  ScanSearch,
} from "lucide-react";

import type { StringKey } from "@/lib/i18n/strings";

export type DomainIconName =
  | "queue"
  | "topic"
  | "subscription"
  | "dead-letter"
  | "namespace"
  | "message-flow"
  | "topology"
  | "discovery"
  | "relay"
  | "environment"
  | "azure-resource";

export interface DomainIconEntry {
  /** The `lucide-react` icon component. */
  readonly icon: LucideIcon;
  /** Consistent stroke weight across the family (FR-021). */
  readonly strokeWidth: number;
  /**
   * i18n key whose value is the accessible label for this icon when used
   * non-decoratively. Composites pass this to `aria-label` via `t(key)`.
   */
  readonly accessibleLabelKey: StringKey | null;
}

const DEFAULT_STROKE_WIDTH = 1.5;

export const DOMAIN_ICONS: Readonly<Record<DomainIconName, DomainIconEntry>> = {
  queue: {
    icon: Inbox,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: "domain.queue.label",
  },
  topic: {
    icon: Megaphone,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: "domain.topic.label",
  },
  subscription: {
    icon: BellRing,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: "domain.subscription.label",
  },
  "dead-letter": {
    icon: MailWarning,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: "domain.deadLetter.label",
  },
  namespace: {
    icon: Boxes,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: "domain.namespace.label",
  },
  "message-flow": {
    icon: ArrowRightLeft,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: null,
  },
  topology: {
    icon: Network,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: null,
  },
  discovery: {
    icon: ScanSearch,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: null,
  },
  relay: {
    icon: Repeat,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: null,
  },
  environment: {
    icon: Globe,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: null,
  },
  "azure-resource": {
    icon: Cloud,
    strokeWidth: DEFAULT_STROKE_WIDTH,
    accessibleLabelKey: null,
  },
};

/**
 * Convenience accessor. Throws (in dev) when an unknown domain name is
 * requested so missing mappings are caught at the first call site.
 */
export function getDomainIcon(name: DomainIconName): DomainIconEntry {
  const entry = DOMAIN_ICONS[name];
  if (!entry) {
    throw new Error(`Unknown domain icon: ${name}`);
  }
  return entry;
}

/**
 * Re-export so composites that need to render a generic alert/error glyph
 * can reach for a single foundation-sanctioned icon (FR-026 — color is
 * always paired with icon + text).
 */
export const AlertGlyph: LucideIcon = AlertOctagon;
