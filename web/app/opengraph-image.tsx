/**
 * Open Graph social preview — Next.js App Router convention (T035).
 *
 * Renders a 1200×630 placeholder preview. See `app/icon.tsx` for the
 * raster-color exception rationale; the final preview replaces this
 * generator in T143.
 */

import { ImageResponse } from "next/og";

import {
  RASTER_FOREGROUND_DEFAULT_DARK,
  RASTER_FOREGROUND_MUTED_DARK,
  RASTER_FOREGROUND_SUBTLE_DARK,
  RASTER_OG_GRADIENT_DARK_A,
  RASTER_OG_GRADIENT_DARK_B,
  RASTER_OG_GRADIENT_DARK_C,
  RASTER_SURFACE_CANVAS_DARK,
} from "@/lib/design-system/raster-colors";

export const alt = "BusTerminal — Operational registry for Azure Service Bus";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpenGraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          justifyContent: "center",
          padding: "80px",
          background: `linear-gradient(135deg, ${RASTER_OG_GRADIENT_DARK_A} 0%, ${RASTER_OG_GRADIENT_DARK_B} 60%, ${RASTER_OG_GRADIENT_DARK_C} 100%)`,
          color: RASTER_FOREGROUND_DEFAULT_DARK,
          fontFamily:
            "ui-sans-serif, system-ui, -apple-system, 'Segoe UI', sans-serif",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            width: 120,
            height: 120,
            borderRadius: 24,
            background: RASTER_SURFACE_CANVAS_DARK,
            border: `2px solid ${RASTER_FOREGROUND_DEFAULT_DARK}`,
            color: RASTER_FOREGROUND_DEFAULT_DARK,
            fontSize: 78,
            fontWeight: 700,
            marginBottom: 36,
          }}
        >
          ╋
        </div>
        <div style={{ fontSize: 88, fontWeight: 700, letterSpacing: -2 }}>
          BusTerminal
        </div>
        <div
          style={{
            fontSize: 32,
            color: RASTER_FOREGROUND_MUTED_DARK,
            marginTop: 16,
            maxWidth: 900,
            lineHeight: 1.3,
          }}
        >
          Operational registry, discovery, governance, and observability for
          Azure Service Bus infrastructure.
        </div>
        <div
          style={{
            fontSize: 20,
            color: RASTER_FOREGROUND_SUBTLE_DARK,
            marginTop: 48,
            fontFamily:
              "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace",
            letterSpacing: 2,
          }}
        >
          PLACEHOLDER · T143 REPLACES THIS
        </div>
      </div>
    ),
    { ...size },
  );
}
