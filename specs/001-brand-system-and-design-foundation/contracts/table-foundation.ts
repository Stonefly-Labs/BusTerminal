/**
 * Data Table Foundation Contract
 *
 * Spec references:
 *   - FR-015 (capabilities: sorting, filtering, column visibility, sticky headers,
 *     keyboard navigation, row actions, multi-select/bulk actions, pagination
 *     OR virtualization, empty/loading/error states, responsive overflow)
 *   - FR-016 (no raw/per-feature tables; all tables consume this foundation)
 *   - Source artifact §11.3
 *
 * Implementation: TanStack Table 8.x for the engine; BusTerminal-owned
 * presentation components and toolbars; lives under `web/components/data-table/`.
 */

import type { ColumnDef, RowSelectionState, SortingState } from '@tanstack/react-table';
import type { ReactNode } from 'react';

export interface DataTableProps<TData, TValue = unknown> {
  /** Strictly-typed column definitions (TanStack ColumnDef). */
  readonly columns: ReadonlyArray<ColumnDef<TData, TValue>>;
  /** The data rows. */
  readonly data: ReadonlyArray<TData>;
  /** Stable, unique row id accessor. Required for selection and column visibility persistence. */
  readonly getRowId: (row: TData, index: number) => string;

  // ----- Capabilities -----
  readonly enableSorting?: boolean; // default: true
  readonly enableColumnFilters?: boolean; // default: true
  readonly enableColumnVisibility?: boolean; // default: true
  readonly enableMultiSelect?: boolean; // default: false
  readonly enableStickyHeader?: boolean; // default: true
  readonly enableKeyboardNavigation?: boolean; // default: true (FR-015 / SC-007)

  // ----- Pagination vs. virtualization (FR-015) -----
  readonly paginationMode: 'paginated' | 'virtualized';

  // ----- State -----
  readonly initialSorting?: SortingState;
  readonly initialRowSelection?: RowSelectionState;

  // ----- State persistence (column visibility, sorting) -----
  readonly persistenceKey?: string; // localStorage key prefix; defaults to none (no persistence)

  // ----- States required by FR-015 -----
  readonly isLoading?: boolean;
  readonly error?: { readonly message: string; readonly retry?: () => void };
  readonly emptyState?: {
    readonly title: string; // sourced from i18n string surface
    readonly description?: string;
    readonly action?: ReactNode;
  };

  // ----- Row actions / bulk actions -----
  readonly rowActions?: (row: TData) => ReadonlyArray<DataTableRowAction>;
  readonly bulkActions?: ReadonlyArray<DataTableBulkAction<TData>>;

  // ----- Toolbar customization -----
  readonly toolbar?: ReactNode;

  // ----- A11y -----
  /** REQUIRED. Sourced from i18n string surface; describes the table contents to assistive tech. */
  readonly caption: string;
}

export interface DataTableRowAction {
  readonly id: string;
  readonly labelKey: string; // i18n string key — never raw text
  readonly icon?: ReactNode;
  readonly destructive?: boolean;
  readonly onSelect: () => void;
}

export interface DataTableBulkAction<TData> {
  readonly id: string;
  readonly labelKey: string;
  readonly icon?: ReactNode;
  readonly destructive?: boolean;
  readonly onSelect: (rows: ReadonlyArray<TData>) => void;
}

/**
 * The DataTable component itself. All product tables MUST consume this
 * foundation rather than building bespoke table chrome (FR-016).
 */
export declare function DataTable<TData, TValue = unknown>(
  props: DataTableProps<TData, TValue>,
): JSX.Element;
