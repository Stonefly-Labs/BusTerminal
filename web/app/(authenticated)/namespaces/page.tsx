/**
 * Spec 008 / T109 / US2. Inventory route shell.
 *
 * Thin RSC shell that mounts the Client Component inventory experience. The
 * `(authenticated)` group above already enforces auth — this page just
 * delegates to the URL-state-driven inventory client.
 */

import { NamespaceInventory } from "@/components/namespaces/inventory/namespace-inventory";

export default function NamespacesInventoryPage() {
  return <NamespaceInventory />;
}
