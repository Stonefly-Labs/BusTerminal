/**
 * BusTerminal i18n string surface — English registry.
 *
 * Implements the contract in
 * `specs/001-brand-system-and-design-foundation/contracts/i18n-strings.ts`.
 *
 * The registry is the single source of truth for every user-facing string
 * inside a primitive or composite. Components MUST call `t(key)` instead of
 * hardcoding copy — enforced by `pnpm audit:strings` (SC-012). A future
 * translation spec swaps the underlying implementation without changing call
 * sites.
 *
 * Key naming convention: `<surface>.<element>.<role>` (dotted, lower-camel).
 *
 * Interpolation slots are declared per-entry under `interpolations`. The
 * `t(key, vars)` accessor substitutes `{varName}` tokens in the English value
 * — locale-aware formatting (dates, numbers, durations, byte counts) flows
 * through `web/lib/i18n/format.ts`, not the string surface.
 */

import type { StringEntry } from "./types";

// -----------------------------------------------------------------------------
// Registry
// -----------------------------------------------------------------------------

const REGISTRY = {
  // --- App shell -----------------------------------------------------------
  "appshell.sidebar.toggle.expand": {
    englishValue: "Expand sidebar",
    description: "Aria label for the sidebar expand button.",
    interpolations: {},
  },
  "appshell.sidebar.toggle.collapse": {
    englishValue: "Collapse sidebar",
    description: "Aria label for the sidebar collapse button.",
    interpolations: {},
  },
  "appshell.topbar.search.placeholder": {
    englishValue: "Search namespaces, queues, topics…",
    description: "Placeholder copy for the top-bar global search trigger.",
    interpolations: {},
  },
  "appshell.topbar.userMenu.label": {
    englishValue: "Open user menu",
    description: "Aria label for the user-menu trigger.",
    interpolations: {},
  },
  "appshell.topbar.themeToggle.label": {
    englishValue: "Toggle theme",
    description: "Aria label for the theme toggle.",
    interpolations: {},
  },
  "appshell.topbar.themeToggle.toLight": {
    englishValue: "Switch to light theme",
    description: "Theme toggle tooltip when current theme is dark.",
    interpolations: {},
  },
  "appshell.topbar.themeToggle.toDark": {
    englishValue: "Switch to dark theme",
    description: "Theme toggle tooltip when current theme is light.",
    interpolations: {},
  },

  // --- Theme labels --------------------------------------------------------
  "theme.light.label": {
    englishValue: "Light",
    description: "Display name for the light theme.",
    interpolations: {},
  },
  "theme.dark.label": {
    englishValue: "Dark",
    description: "Display name for the dark theme.",
    interpolations: {},
  },
  "theme.system.label": {
    englishValue: "System",
    description: "Display name for the system-preference resolver state.",
    interpolations: {},
  },

  // --- Navigation ----------------------------------------------------------
  "navigation.breadcrumb.separator": {
    englishValue: "/",
    description: "Separator glyph between breadcrumb segments.",
    interpolations: {},
  },
  "navigation.commandPalette.trigger.label": {
    englishValue: "Command palette",
    description: "Aria label for the command-palette trigger.",
    interpolations: {},
  },
  "navigation.commandPalette.shortcut": {
    englishValue: "Press {shortcut} to open the command palette.",
    description: "Tooltip describing the command-palette keyboard shortcut.",
    interpolations: { shortcut: "string" },
  },
  "navigation.pagination.previous": {
    englishValue: "Previous",
    description: "Pagination previous-page button label.",
    interpolations: {},
  },
  "navigation.pagination.next": {
    englishValue: "Next",
    description: "Pagination next-page button label.",
    interpolations: {},
  },
  "navigation.pagination.pageStatus": {
    englishValue: "Page {page} of {total}",
    description: "Pagination current-page status announcement.",
    interpolations: { page: "number", total: "number" },
  },

  // --- Tables --------------------------------------------------------------
  "table.toolbar.search.placeholder": {
    englishValue: "Search…",
    description: "Default placeholder for the data-table search input.",
    interpolations: {},
  },
  "table.toolbar.bulkActions.label": {
    englishValue: "Bulk actions",
    description: "Aria label for the bulk-actions menu trigger.",
    interpolations: {},
  },
  "table.toolbar.columnVisibility.label": {
    englishValue: "Columns",
    description: "Label for the column-visibility menu trigger.",
    interpolations: {},
  },
  "table.toolbar.bulkActions.selected": {
    englishValue: "{count} selected",
    description: "Toolbar selection counter.",
    interpolations: { count: "number" },
  },
  "table.row.select.label": {
    englishValue: "Select row",
    description: "Aria label for the row-selection checkbox.",
    interpolations: {},
  },
  "table.row.selectAll.label": {
    englishValue: "Select all rows on this page",
    description: "Aria label for the header select-all checkbox.",
    interpolations: {},
  },
  "table.empty.title": {
    englishValue: "No results",
    description: "Title shown when the table has zero rows.",
    interpolations: {},
  },
  "table.empty.description": {
    englishValue: "Adjust filters or refresh the underlying data.",
    description: "Body copy shown when the table has zero rows.",
    interpolations: {},
  },
  "table.error.title": {
    englishValue: "Couldn't load data",
    description: "Title shown when table data fails to load.",
    interpolations: {},
  },
  "table.error.description": {
    englishValue: "An error occurred while fetching this table's contents.",
    description: "Body copy shown when table data fails to load.",
    interpolations: {},
  },
  "table.error.retry": {
    englishValue: "Try again",
    description: "Retry button on the table error state.",
    interpolations: {},
  },
  "table.loading.label": {
    englishValue: "Loading…",
    description: "Aria label for the table loading state.",
    interpolations: {},
  },

  // --- Dialogs / overlays --------------------------------------------------
  "dialog.close": {
    englishValue: "Close",
    description: "Aria label for a dialog close button.",
    interpolations: {},
  },
  "dialog.destructive.confirmLabel": {
    englishValue: "Confirm",
    description: "Default confirm-label for a destructive dialog.",
    interpolations: {},
  },
  "dialog.destructive.cancelLabel": {
    englishValue: "Cancel",
    description: "Default cancel-label for a destructive dialog.",
    interpolations: {},
  },
  "dialog.destructive.defaultTitle": {
    englishValue: "Are you sure?",
    description: "Default destructive-dialog title.",
    interpolations: {},
  },
  "dialog.destructive.defaultDescription": {
    englishValue: "This action cannot be undone.",
    description: "Default destructive-dialog description.",
    interpolations: {},
  },
  "sheet.close": {
    englishValue: "Close panel",
    description: "Aria label for a sheet/drawer close button.",
    interpolations: {},
  },

  // --- Command palette -----------------------------------------------------
  "command.placeholder": {
    englishValue: "Type a command or search…",
    description: "Placeholder for the command-palette input.",
    interpolations: {},
  },
  "command.empty": {
    englishValue: "No results found.",
    description: "Empty-state copy for the command palette.",
    interpolations: {},
  },

  // --- Toasts --------------------------------------------------------------
  "toast.dismiss": {
    englishValue: "Dismiss",
    description: "Aria label for the toast dismiss button.",
    interpolations: {},
  },
  "toast.success.defaultTitle": {
    englishValue: "Success",
    description: "Default toast title for the success variant.",
    interpolations: {},
  },
  "toast.error.defaultTitle": {
    englishValue: "Error",
    description: "Default toast title for the error variant.",
    interpolations: {},
  },
  "toast.warning.defaultTitle": {
    englishValue: "Warning",
    description: "Default toast title for the warning variant.",
    interpolations: {},
  },
  "toast.info.defaultTitle": {
    englishValue: "Info",
    description: "Default toast title for the info variant.",
    interpolations: {},
  },

  // --- Forms ---------------------------------------------------------------
  "form.required.indicator": {
    englishValue: "Required",
    description: "Accessible label for the required-field indicator.",
    interpolations: {},
  },
  "form.optional.indicator": {
    englishValue: "Optional",
    description: "Accessible label for the optional-field indicator.",
    interpolations: {},
  },
  "form.submit.default": {
    englishValue: "Save",
    description: "Default submit-button copy.",
    interpolations: {},
  },
  "form.submit.pending": {
    englishValue: "Saving…",
    description: "Submit-button copy while a submission is in flight.",
    interpolations: {},
  },
  "form.error.summary": {
    englishValue: "Please fix the errors below before submitting.",
    description: "Top-of-form error-summary copy.",
    interpolations: {},
  },

  // --- Feedback primitives -------------------------------------------------
  "feedback.empty.defaultTitle": {
    englishValue: "Nothing here yet",
    description: "Default empty-state title.",
    interpolations: {},
  },
  "feedback.empty.defaultDescription": {
    englishValue: "Once data is available, it will appear here.",
    description: "Default empty-state description.",
    interpolations: {},
  },
  "feedback.error.defaultTitle": {
    englishValue: "Something went wrong",
    description: "Default error-state title.",
    interpolations: {},
  },
  "feedback.error.defaultDescription": {
    englishValue: "We couldn't complete that request.",
    description: "Default error-state description.",
    interpolations: {},
  },
  "feedback.retry.label": {
    englishValue: "Retry",
    description: "Retry affordance label.",
    interpolations: {},
  },

  // --- Domain composites ---------------------------------------------------
  "domain.namespace.label": {
    englishValue: "Namespace",
    description: "Display label for the Service Bus namespace concept.",
    interpolations: {},
  },
  "domain.queue.label": {
    englishValue: "Queue",
    description: "Display label for the Service Bus queue concept.",
    interpolations: {},
  },
  "domain.topic.label": {
    englishValue: "Topic",
    description: "Display label for the Service Bus topic concept.",
    interpolations: {},
  },
  "domain.subscription.label": {
    englishValue: "Subscription",
    description: "Display label for the Service Bus subscription concept.",
    interpolations: {},
  },
  "domain.deadLetter.label": {
    englishValue: "Dead-letter",
    description: "Display label for the dead-letter queue concept.",
    interpolations: {},
  },
  "domain.deadLetter.count": {
    englishValue: "{count} dead-lettered",
    description: "Dead-letter indicator copy showing the count.",
    interpolations: { count: "number" },
  },
  "domain.messageCount.label": {
    englishValue: "{count} messages",
    description: "Message-count indicator copy.",
    interpolations: { count: "number" },
  },
  "domain.health.healthy": {
    englishValue: "Healthy",
    description: "Healthy state for the health summary indicator.",
    interpolations: {},
  },
  "domain.health.degraded": {
    englishValue: "Degraded",
    description: "Degraded state for the health summary indicator.",
    interpolations: {},
  },
  "domain.health.unhealthy": {
    englishValue: "Unhealthy",
    description: "Unhealthy state for the health summary indicator.",
    interpolations: {},
  },
  "domain.discoveryJob.running": {
    englishValue: "Running",
    description: "Discovery job state label.",
    interpolations: {},
  },
  "domain.discoveryJob.succeeded": {
    englishValue: "Succeeded",
    description: "Discovery job state label.",
    interpolations: {},
  },
  "domain.discoveryJob.failed": {
    englishValue: "Failed",
    description: "Discovery job state label.",
    interpolations: {},
  },
  "domain.discoveryJob.queued": {
    englishValue: "Queued",
    description: "Discovery job state label.",
    interpolations: {},
  },
  "domain.discoveryJob.startedAgo": {
    englishValue: "Started {ago}",
    description: "Discovery job relative-time label (ago = formatted by formatRelativeTime).",
    interpolations: { ago: "string" },
  },
  "domain.environment.dev": {
    englishValue: "Development",
    description: "Environment-badge label.",
    interpolations: {},
  },
  "domain.environment.test": {
    englishValue: "Test",
    description: "Environment-badge label.",
    interpolations: {},
  },
  "domain.environment.staging": {
    englishValue: "Staging",
    description: "Environment-badge label.",
    interpolations: {},
  },
  "domain.environment.prod": {
    englishValue: "Production",
    description: "Environment-badge label.",
    interpolations: {},
  },
  "domain.environment.announce": {
    englishValue: "Environment: {environment}",
    description: "Screen-reader announcement for the environment badge.",
    interpolations: { environment: "string" },
  },
  "domain.azureResource.openLabel": {
    englishValue: "Open in Azure portal",
    description: "Aria label for the Azure resource external link.",
    interpolations: {},
  },
  "domain.azureResource.copyLabel": {
    englishValue: "Copy resource ID",
    description: "Aria label for the Azure resource copy affordance.",
    interpolations: {},
  },
  "domain.metadata.empty": {
    englishValue: "No metadata recorded.",
    description: "Empty state for the metadata key-value panel.",
    interpolations: {},
  },
  "domain.topology.placeholder": {
    englishValue: "Topology visualization will be added by a future spec.",
    description: "Inert placeholder copy for the topology mini-map.",
    interpolations: {},
  },

  // --- Accessibility -------------------------------------------------------
  "a11y.skipToContent": {
    englishValue: "Skip to main content",
    description: "Skip-link copy targeting the page's main landmark.",
    interpolations: {},
  },
  "a11y.loading": {
    englishValue: "Loading",
    description: "Generic aria-busy label.",
    interpolations: {},
  },
  "a11y.required": {
    englishValue: "(required)",
    description: "Screen-reader-only suffix for required fields.",
    interpolations: {},
  },

  // --- Error boundary ------------------------------------------------------
  "error.boundary.title": {
    englishValue: "Something went wrong",
    description: "Title for the top-level error surface.",
    interpolations: {},
  },
  "error.boundary.description": {
    englishValue:
      "The page hit an unexpected error. The team has been notified; you can retry or return to the dashboard.",
    description: "Body copy for the top-level error surface.",
    interpolations: {},
  },
  "error.boundary.retry": {
    englishValue: "Try again",
    description: "Retry affordance on the top-level error surface.",
    interpolations: {},
  },
  "error.boundary.home": {
    englishValue: "Go to dashboard",
    description: "Fallback navigation affordance on the error surface.",
    interpolations: {},
  },
} as const satisfies Record<string, Omit<StringEntry, "key">>;

// -----------------------------------------------------------------------------
// Public surface
// -----------------------------------------------------------------------------

export type StringKey = keyof typeof REGISTRY;

type EntryVars<K extends StringKey> = (typeof REGISTRY)[K]["interpolations"];

type HasVars<K extends StringKey> = keyof EntryVars<K> extends never ? false : true;

type VarsArg<K extends StringKey> = HasVars<K> extends true
  ? { [P in keyof EntryVars<K>]: string | number | Date }
  : Record<string, never> | undefined;

/**
 * Resolve a registered string key to its English value, substituting
 * interpolation slots when present.
 *
 * The compiler enforces:
 *   - Only registered keys are accepted.
 *   - Entries that declare interpolations require a matching `vars` argument.
 *   - Entries with no interpolations reject any `vars` argument.
 */
export function t<K extends StringKey>(
  key: K,
  ...rest: HasVars<K> extends true ? [vars: VarsArg<K>] : []
): string {
  const entry = REGISTRY[key];
  const vars = rest[0] as Record<string, string | number | Date> | undefined;
  if (!vars) return entry.englishValue;
  return entry.englishValue.replace(/\{(\w+)\}/g, (match, varName: string) => {
    if (!(varName in vars)) return match;
    const value = vars[varName];
    if (value instanceof Date) return value.toISOString();
    return String(value);
  });
}

/**
 * Enumerated list of every registered key. Consumed by
 * `scripts/audit-strings.mjs` (SC-012).
 */
export const ALL_STRING_KEYS: readonly StringKey[] = Object.keys(REGISTRY) as StringKey[];
