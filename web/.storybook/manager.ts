/**
 * Storybook manager (sidebar/toolbar) configuration. Applies the
 * brand-aligned UI theme defined in `./theme.ts`.
 */

import { addons } from "storybook/manager-api";

import busTerminalTheme from "./theme";

addons.setConfig({
  theme: busTerminalTheme,
  toolbar: {
    title: { hidden: false },
    zoom: { hidden: false },
  },
});
