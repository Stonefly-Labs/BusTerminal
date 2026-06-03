import { describe, expect, it } from "vitest";

import {
  distinctTagKeysLower,
  groupTagsByKeyLower,
  normalizeTagsForWrite,
  tagEquals,
  tagKeyLower,
} from "../tag-utils";

describe("tagKeyLower", () => {
  it("lowercases ASCII keys", () => {
    expect(tagKeyLower("Owner")).toBe("owner");
  });
});

describe("tagEquals", () => {
  it("matches keys case-insensitively and values case-sensitively", () => {
    expect(tagEquals({ key: "Owner", value: "Alice" }, { key: "owner", value: "Alice" })).toBe(true);
    expect(tagEquals({ key: "Owner", value: "Alice" }, { key: "owner", value: "alice" })).toBe(false);
  });
});

describe("distinctTagKeysLower", () => {
  it("de-duplicates and lowercases", () => {
    const tags = [
      { key: "Owner", value: "Alice" },
      { key: "owner", value: "Bob" },
      { key: "Tier", value: "1" },
    ];
    expect(distinctTagKeysLower(tags)).toEqual(["owner", "tier"]);
  });
});

describe("normalizeTagsForWrite", () => {
  it("uses persisted casing when present", () => {
    const persisted = [{ key: "Owner", value: "PaymentsTeam" }];
    const submitted = [{ key: "OWNER", value: "RecentlyChanged" }];
    expect(normalizeTagsForWrite(submitted, persisted)).toEqual([
      { key: "Owner", value: "RecentlyChanged" },
    ]);
  });

  it("first-write wins within the submission when no persisted tag matches", () => {
    const submitted = [
      { key: "Owner", value: "Alice" },
      { key: "OWNER", value: "Bob" },
    ];
    expect(normalizeTagsForWrite(submitted, undefined)).toEqual([
      { key: "Owner", value: "Alice" },
      { key: "Owner", value: "Bob" },
    ]);
  });

  it("returns empty for empty submission", () => {
    expect(normalizeTagsForWrite([], undefined)).toEqual([]);
  });
});

describe("groupTagsByKeyLower", () => {
  it("groups multi-value tags", () => {
    const tags = [
      { key: "Owner", value: "Alice" },
      { key: "owner", value: "Bob" },
      { key: "Tier", value: "1" },
    ];
    const groups = groupTagsByKeyLower(tags);
    expect(groups.get("owner")).toHaveLength(2);
    expect(groups.get("tier")).toHaveLength(1);
  });
});
