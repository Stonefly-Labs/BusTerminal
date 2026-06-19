import { describe, expect, it } from "vitest";

import { canEditEntityMetadata } from "../../discovery/permissions";
import type { EntityServiceAssociation } from "../../discovery/schemas";
import {
  authorizedRoles,
  isAuthorized,
  OPERATION_CLASSES,
  parseRole,
  PLATFORM_ROLES,
  type OperationClass,
  type PlatformRole,
} from "../role-permission-matrix";

const EXPECTED: Record<OperationClass, readonly PlatformRole[]> = {
  Read: [
    "BusTerminal.Reader",
    "BusTerminal.Developer",
    "BusTerminal.Operator",
    "BusTerminal.Admin",
    // Spec 008 — NamespaceAdministrator implicitly grants Read. See
    // role-permission-matrix.ts for rationale.
    "BusTerminal.NamespaceAdministrator",
  ],
  MutateDomain: ["BusTerminal.Operator", "BusTerminal.Admin"],
  OperatePlatform: ["BusTerminal.Operator", "BusTerminal.Admin"],
  Administer: ["BusTerminal.Admin"],
  DeveloperTooling: ["BusTerminal.Developer", "BusTerminal.Admin"],
  // Spec 009 / R-15 — only the role-only branches live in the matrix; the
  // third branch (Service Owner of an Owner-role association) is contextual
  // and verified separately via `canEditEntityMetadata`.
  EditEntityMetadata: ["BusTerminal.Admin", "BusTerminal.NamespaceAdministrator"],
};

describe("role-permission-matrix", () => {
  it("enumerates every operation class", () => {
    expect([...OPERATION_CLASSES].sort()).toEqual(
      (Object.keys(EXPECTED) as OperationClass[]).sort(),
    );
  });

  it("enumerates every platform role", () => {
    expect([...PLATFORM_ROLES].sort()).toEqual(
      [
        "BusTerminal.Admin",
        "BusTerminal.Operator",
        "BusTerminal.Reader",
        "BusTerminal.Developer",
        "BusTerminal.NamespaceAdministrator",
      ].sort(),
    );
  });

  for (const operationClass of OPERATION_CLASSES) {
    it(`maps ${operationClass} to the expected role set`, () => {
      expect([...authorizedRoles(operationClass)].sort()).toEqual(
        [...EXPECTED[operationClass]].sort(),
      );
    });
  }

  it("parseRole accepts the four known role values", () => {
    for (const value of PLATFORM_ROLES) {
      expect(parseRole(value)).toBe(value);
    }
  });

  it("parseRole returns null for unknown role values", () => {
    expect(parseRole("BusTerminal.SuperAdmin")).toBeNull();
    expect(parseRole("admin")).toBeNull();
    expect(parseRole("")).toBeNull();
  });

  it("isAuthorized matches the matrix for every (role, class) pair", () => {
    for (const role of PLATFORM_ROLES) {
      const roleSet: ReadonlySet<PlatformRole> = new Set([role]);
      for (const operationClass of OPERATION_CLASSES) {
        const expected = EXPECTED[operationClass].includes(role);
        expect(isAuthorized(roleSet, operationClass)).toBe(expected);
      }
    }
  });

  it("isAuthorized rejects every operation class for a roleless caller", () => {
    const noRoles: ReadonlySet<PlatformRole> = new Set();
    for (const operationClass of OPERATION_CLASSES) {
      expect(isAuthorized(noRoles, operationClass)).toBe(false);
    }
  });

  // Spec 009 / T118 — the role-only branches of the EditEntityMetadata
  // operation class MUST agree with `canEditEntityMetadata`. The contextual
  // third branch (Service Owner of an Owner-role association) is verified
  // separately below.
  describe("EditEntityMetadata aligns with canEditEntityMetadata", () => {
    const ownerAssoc: EntityServiceAssociation = {
      associationId: "esa_owner",
      serviceId: "svc_owner",
      role: "Owner",
      createdUtc: "2026-06-18T00:00:00.000Z",
      createdBy: "operator",
    };
    const producerAssoc: EntityServiceAssociation = {
      associationId: "esa_producer",
      serviceId: "svc_producer",
      role: "Producer",
      createdUtc: "2026-06-18T00:00:00.000Z",
      createdBy: "operator",
    };

    for (const role of PLATFORM_ROLES) {
      it(`role-only branch agrees for ${role}`, () => {
        const roleContext = { roles: new Set<string>([role]) };
        const matrixAllow = isAuthorized(
          new Set<PlatformRole>([role]),
          "EditEntityMetadata",
        );
        const helperAllow = canEditEntityMetadata(
          { serviceAssociations: [] },
          roleContext,
          new Set<string>(),
        );
        expect(helperAllow).toBe(matrixAllow);
      });
    }

    it("contextual branch (Owner-role assoc on an owned service) allows the caller", () => {
      const reader = { roles: new Set<string>(["BusTerminal.Reader"]) };
      // No Owner-role assoc → matrix-equivalent answer (false).
      expect(
        canEditEntityMetadata({ serviceAssociations: [] }, reader, new Set([ownerAssoc.serviceId])),
      ).toBe(false);
      // Producer-role assoc on owned service → still no (FR allow requires Owner role).
      expect(
        canEditEntityMetadata(
          { serviceAssociations: [producerAssoc] },
          reader,
          new Set([producerAssoc.serviceId]),
        ),
      ).toBe(false);
      // Owner-role assoc on owned service → allow.
      expect(
        canEditEntityMetadata(
          { serviceAssociations: [ownerAssoc] },
          reader,
          new Set([ownerAssoc.serviceId]),
        ),
      ).toBe(true);
    });

    it("contextual branch is INDEPENDENT of the matrix Admin/NamespaceAdmin paths", () => {
      // Admin always allows regardless of associations / ownership.
      const admin = { roles: new Set<string>(["BusTerminal.Admin"]) };
      expect(
        canEditEntityMetadata({ serviceAssociations: [] }, admin, new Set<string>()),
      ).toBe(true);
      // NamespaceAdministrator likewise.
      const namespaceAdmin = {
        roles: new Set<string>(["BusTerminal.NamespaceAdministrator"]),
      };
      expect(
        canEditEntityMetadata({ serviceAssociations: [] }, namespaceAdmin, new Set<string>()),
      ).toBe(true);
    });
  });
});
