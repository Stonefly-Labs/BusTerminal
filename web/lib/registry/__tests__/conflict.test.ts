import { describe, expect, it } from "vitest";

import { diffEntities, parseConflictResponse } from "../conflict";
import type { RegistryEntity } from "../schemas";

const baseEntity: RegistryEntity = {
  id: "9c8f3b1a-1234-4abc-8def-1234567890ab",
  entityType: "Queue",
  name: "orders-incoming",
  environment: "dev",
  status: "Active",
  createdAtUtc: "2026-06-02T14:00:00.000Z",
  updatedAtUtc: "2026-06-02T14:00:00.000Z",
  source: "Manual",
  tags: [],
  parentId: "5e3c2a7d-2222-4cde-9f01-abcdef012345",
};

describe("diffEntities", () => {
  it("returns no changes for identical entities", () => {
    expect(diffEntities(baseEntity, baseEntity)).toEqual([]);
  });

  it("detects scalar diff on description", () => {
    const submitted = { ...baseEntity, description: "after" };
    const current = { ...baseEntity, description: "before" };
    const changes = diffEntities(current, submitted);
    expect(changes).toHaveLength(1);
    expect(changes[0]).toMatchObject({
      field: "description",
      currentValue: "before",
      submittedValue: "after",
    });
  });

  it("excludes server-managed fields from the diff", () => {
    const current = {
      ...baseEntity,
      updatedAtUtc: "2026-06-02T14:00:00.000Z",
      fullyQualifiedName: "orders-prod/orders-incoming",
    };
    const submitted = {
      ...baseEntity,
      updatedAtUtc: "2026-06-02T14:00:05.000Z",
      fullyQualifiedName: "orders-prod/orders-incoming-renamed",
    };
    expect(diffEntities(current, submitted)).toEqual([]);
  });

  it("detects tag-array diff (multi-value-per-key)", () => {
    const current = {
      ...baseEntity,
      tags: [
        { key: "Owner", value: "Alice" },
        { key: "Owner", value: "Bob" },
      ],
    };
    const submitted = { ...baseEntity, tags: [{ key: "Owner", value: "Alice" }] };
    const changes = diffEntities(current, submitted);
    expect(changes).toHaveLength(1);
    expect(changes[0]?.field).toBe("tags");
  });

  it("detects nested metadata diff", () => {
    const current = {
      ...baseEntity,
      metadata: { policy: { retention: { days: 30 } } },
    };
    const submitted = {
      ...baseEntity,
      metadata: { policy: { retention: { days: 60 } } },
    };
    const changes = diffEntities(current, submitted);
    expect(changes).toHaveLength(1);
    expect(changes[0]?.field).toBe("metadata");
  });

  it("detects null↔value transition", () => {
    const current = { ...baseEntity, description: undefined };
    const submitted = { ...baseEntity, description: "added" };
    const changes = diffEntities(current, submitted);
    expect(changes).toHaveLength(1);
    expect(changes[0]?.field).toBe("description");
  });
});

describe("parseConflictResponse", () => {
  it("returns null for non-Error inputs", async () => {
    expect(await parseConflictResponse(undefined)).toBeNull();
    expect(await parseConflictResponse("oops")).toBeNull();
  });

  it("returns null when status is not 409", async () => {
    const err = new Error("server-error") as Error & { status?: number };
    err.status = 500;
    expect(await parseConflictResponse(err, "{}")).toBeNull();
  });

  it("parses a valid conflict body", async () => {
    const err = new Error("conflict") as Error & { status?: number };
    err.status = 409;
    const body = {
      type: "https://busterminal.dev/probs/concurrency-conflict",
      title: "Concurrency conflict",
      status: 409,
      code: "ConcurrencyConflict",
      entityId: baseEntity.id,
      currentVersion: '"v1"',
      submittedVersion: '"v0"',
      currentEntity: baseEntity,
      changedFields: [{ field: "description", currentValue: "a", submittedValue: "b" }],
    };
    const parsed = await parseConflictResponse(err, JSON.stringify(body));
    expect(parsed).not.toBeNull();
    expect(parsed!.code).toBe("ConcurrencyConflict");
    expect(parsed!.changedFields).toHaveLength(1);
  });
});
