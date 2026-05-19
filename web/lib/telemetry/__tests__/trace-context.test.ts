import { describe, expect, it } from "vitest";
import {
  generateTraceparent,
  getOrCreateActiveTraceContext,
  __resetActiveTraceContextForTests,
  parseTraceparent,
  serializeTraceparent,
} from "@/lib/telemetry/trace-context";

const TRACEPARENT_REGEX = /^00-[0-9a-f]{32}-[0-9a-f]{16}-(00|01)$/;

describe("generateTraceparent", () => {
  it("produces a value matching the W3C traceparent format", () => {
    const value = generateTraceparent();
    expect(value).toMatch(TRACEPARENT_REGEX);
  });

  it("emits unique values across invocations", () => {
    const seen = new Set<string>();
    for (let i = 0; i < 100; i += 1) {
      seen.add(generateTraceparent());
    }
    expect(seen.size).toBe(100);
  });

  it("rejects zero trace IDs (W3C invalid)", () => {
    const value = generateTraceparent();
    const parsed = parseTraceparent(value);
    expect(parsed).not.toBeNull();
    expect(/^0+$/.test(parsed!.traceId)).toBe(false);
    expect(/^0+$/.test(parsed!.spanId)).toBe(false);
  });
});

describe("getOrCreateActiveTraceContext", () => {
  it("returns a stable context across calls", () => {
    __resetActiveTraceContextForTests();
    const first = getOrCreateActiveTraceContext();
    const second = getOrCreateActiveTraceContext();
    expect(first).toBe(second);
  });

  it("regenerates after reset", () => {
    __resetActiveTraceContextForTests();
    const first = getOrCreateActiveTraceContext();
    __resetActiveTraceContextForTests();
    const second = getOrCreateActiveTraceContext();
    expect(serializeTraceparent(first)).not.toBe(serializeTraceparent(second));
  });
});
