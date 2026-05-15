/**
 * Foundation placeholder home page.
 *
 * This page intentionally renders the minimum needed to verify the design
 * tokens, theme provider, and global CSS layer are wired correctly. It is
 * replaced by the representative composed demo screen in T099 once the US1
 * primitive set is published.
 */

export default function Home() {
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-6 px-8 py-16">
      <h1 className="text-3xl font-semibold tracking-tight">BusTerminal</h1>
      <p className="max-w-prose text-center text-sm text-foreground-muted">
        Brand system and design foundation — Phase 2 (foundational prerequisites) in
        progress. The composed demo screen lands in T099.
      </p>
    </main>
  );
}
