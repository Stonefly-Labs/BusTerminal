/**
 * Apple touch icon — Next.js App Router convention (T034).
 * See `app/icon.tsx` for the raster-color exception rationale.
 */

import { ImageResponse } from "next/og";

import {
  RASTER_FOREGROUND_DEFAULT_DARK,
  RASTER_SURFACE_CANVAS_DARK,
} from "@/lib/design-system/raster-colors";

export const size = { width: 180, height: 180 };
export const contentType = "image/png";

export default function AppleIcon() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: RASTER_SURFACE_CANVAS_DARK,
          color: RASTER_FOREGROUND_DEFAULT_DARK,
          borderRadius: 32,
          fontSize: 110,
          fontWeight: 700,
          fontFamily:
            "ui-sans-serif, system-ui, -apple-system, 'Segoe UI', sans-serif",
        }}
      >
        ╋
      </div>
    ),
    { ...size },
  );
}
