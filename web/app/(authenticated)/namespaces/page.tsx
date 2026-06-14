/**
 * Spec 008 / T003 — placeholder inventory route.
 *
 * The real Inventory implementation (Server Component composing
 * `<NamespaceInventoryFilters>` + `<NamespaceInventoryTable>`) lands in
 * T109 (Phase 4). This stub exists so the route compiles before then.
 */

export default function NamespacesInventoryPlaceholderPage() {
  return (
    <div className="p-6">
      <h1 className="text-lg font-semibold text-foreground-default">Namespaces</h1>
      <p className="mt-2 text-sm text-foreground-muted">
        Namespace inventory will render here once spec 008 Phase 4 (User Story 2) lands.
      </p>
    </div>
  );
}
