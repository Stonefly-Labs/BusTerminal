import type { Metadata, Viewport } from "next";
import Script from "next/script";
import { Geist, Geist_Mono } from "next/font/google";

import "@/lib/observability/register-adapters";
import { Providers } from "./providers";
import { MsalProvider } from "@/components/auth/msal-provider";
import { directionForLocale } from "@/lib/i18n";
import { THEME_STORAGE_KEY } from "@/lib/theme-provider-constants";
import {
  RASTER_SURFACE_CANVAS_DARK,
  RASTER_SURFACE_CANVAS_LIGHT,
} from "@/lib/design-system/raster-colors";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
  display: "swap",
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
  display: "swap",
});

const DEFAULT_LOCALE = "en";
const DEFAULT_DIRECTION = directionForLocale(DEFAULT_LOCALE);

export const metadata: Metadata = {
  title: {
    default: "BusTerminal",
    template: "%s · BusTerminal",
  },
  description:
    "Operational registry, discovery, governance, and observability for Azure Service Bus infrastructure.",
  applicationName: "BusTerminal",
  // Favicons and apple-icon are auto-detected from `app/icon.tsx` and
  // `app/apple-icon.tsx`. The static exported PNG set in
  // `web/public/favicons/` lands in T142 and the explicit `icons` block
  // is reintroduced at that time.
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: RASTER_SURFACE_CANVAS_LIGHT },
    { media: "(prefers-color-scheme: dark)", color: RASTER_SURFACE_CANVAS_DARK },
  ],
};

/**
 * Inline anti-FOUC script. Runs synchronously in <head> before paint so the
 * resolved theme class is on <html> when first pixels render — satisfies
 * SC-004 (no flash of incorrect theme on first load).
 */
const ANTI_FOUC_SCRIPT = `
(function() {
  try {
    var storageKey = ${JSON.stringify(THEME_STORAGE_KEY)};
    var stored = window.localStorage.getItem(storageKey);
    var systemDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    var theme = stored === 'light' || stored === 'dark'
      ? stored
      : (systemDark ? 'dark' : 'light');
    if (theme === 'dark') {
      document.documentElement.classList.add('dark');
    }
    document.documentElement.style.colorScheme = theme;
  } catch (err) {
    /* no-op; ThemeProvider will resolve on hydration */
  }
})();
`;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang={DEFAULT_LOCALE}
      dir={DEFAULT_DIRECTION}
      suppressHydrationWarning
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <head>
        <Script
          id="bt-anti-fouc"
          strategy="beforeInteractive"
          dangerouslySetInnerHTML={{ __html: ANTI_FOUC_SCRIPT }}
        />
      </head>
      <body className="min-h-full flex flex-col bg-surface-canvas text-foreground-default">
        <MsalProvider>
          <Providers>{children}</Providers>
        </MsalProvider>
      </body>
    </html>
  );
}
