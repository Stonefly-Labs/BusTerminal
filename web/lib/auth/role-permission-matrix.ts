// Mirror of specs/003-auth-and-identity/contracts/role-permission-matrix.md.
// The contract document is authoritative; this module is its code projection.

// Spec 008 adds a fifth role (`BusTerminal.NamespaceAdministrator`) as an
// additive extension — the existing four roles retain their spec-003
// semantics unchanged. The new role gates namespace write surfaces only
// (`/api/namespaces/*` mutating endpoints).
export type PlatformRole =
  | "BusTerminal.Admin"
  | "BusTerminal.Operator"
  | "BusTerminal.Reader"
  | "BusTerminal.Developer"
  | "BusTerminal.NamespaceAdministrator";

export const PLATFORM_ROLES: readonly PlatformRole[] = [
  "BusTerminal.Admin",
  "BusTerminal.Operator",
  "BusTerminal.Reader",
  "BusTerminal.Developer",
  "BusTerminal.NamespaceAdministrator",
];

const KNOWN_ROLE_VALUES = new Set<string>(PLATFORM_ROLES);

export function parseRole(value: string): PlatformRole | null {
  return KNOWN_ROLE_VALUES.has(value) ? (value as PlatformRole) : null;
}

export type OperationClass =
  | "Read"
  | "MutateDomain"
  | "OperatePlatform"
  | "Administer"
  | "DeveloperTooling";

export const OPERATION_CLASSES: readonly OperationClass[] = [
  "Read",
  "MutateDomain",
  "OperatePlatform",
  "Administer",
  "DeveloperTooling",
];

const MATRIX: Readonly<Record<OperationClass, readonly PlatformRole[]>> = {
  Read: [
    "BusTerminal.Reader",
    "BusTerminal.Developer",
    "BusTerminal.Operator",
    "BusTerminal.Admin",
  ],
  MutateDomain: ["BusTerminal.Operator", "BusTerminal.Admin"],
  OperatePlatform: ["BusTerminal.Operator", "BusTerminal.Admin"],
  Administer: ["BusTerminal.Admin"],
  DeveloperTooling: ["BusTerminal.Developer", "BusTerminal.Admin"],
};

export function authorizedRoles(operationClass: OperationClass): readonly PlatformRole[] {
  return MATRIX[operationClass];
}

export function isAuthorized(
  roles: ReadonlySet<PlatformRole>,
  operationClass: OperationClass,
): boolean {
  return authorizedRoles(operationClass).some((role) => roles.has(role));
}
