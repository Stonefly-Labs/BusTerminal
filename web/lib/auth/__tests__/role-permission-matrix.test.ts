import { describe, expect, it } from "vitest";

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
  ],
  MutateDomain: ["BusTerminal.Operator", "BusTerminal.Admin"],
  OperatePlatform: ["BusTerminal.Operator", "BusTerminal.Admin"],
  Administer: ["BusTerminal.Admin"],
  DeveloperTooling: ["BusTerminal.Developer", "BusTerminal.Admin"],
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
});
