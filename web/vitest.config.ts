import { defineConfig } from "vitest/config";
import path from "node:path";

export default defineConfig({
  resolve: {
    alias: {
      "@": path.resolve(__dirname),
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./tests/setup-vitest.ts"],
    include: [
      "tests/unit/**/*.{test,spec}.{ts,tsx}",
      "tests/auth/**/*.{test,spec}.{ts,tsx}",
      "components/**/*.{test,spec}.{ts,tsx}",
      "lib/**/*.{test,spec}.{ts,tsx}",
      "hooks/**/*.{test,spec}.{ts,tsx}",
    ],
    exclude: ["node_modules", ".next", "tests/e2e/**", "tests/a11y/**"],
    css: true,
    passWithNoTests: true,
  },
});
