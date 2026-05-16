/**
 * Favicon — Next.js App Router convention (T034).
 *
 * Renders the placeholder glyph at 32×32 via `ImageResponse`. The real
 * exported favicon set will land in `web/public/favicons/` in T142–T143 and
 * the metadata wiring in `app/layout.tsx` will switch over then.
 *
 * `ImageResponse` cannot read CSS custom properties, so raw color values
 * come from `@/lib/design-system/raster-colors` — the only sanctioned
 * raster-rendering exception to the token discipline.
 */

import { ImageResponse } from "next/og";

import {
  RASTER_FOREGROUND_DEFAULT_DARK,
  RASTER_SURFACE_CANVAS_DARK,
} from "@/lib/design-system/raster-colors";

export const size = { width: 32, height: 32 };
export const contentType = "image/png";

export default function Icon() {
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
          borderRadius: 6,
          fontSize: 22,
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
