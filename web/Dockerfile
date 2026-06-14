# syntax=docker/dockerfile:1.7

# --- Build stage ---
FROM node:26-alpine AS build
WORKDIR /app

ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
# CI=true makes pnpm non-interactive — without it, `pnpm install` aborts
# at the "Aborted removal of modules directory due to no TTY" prompt
# during the build stage's stale-check.
ENV CI=true
# Node 26 unbundled corepack (https://nodejs.org/api/corepack.html — see
# Node 25 changelog). Install it explicitly before enabling; the
# pnpm version is still resolved from package.json#packageManager.
RUN npm install -g corepack@latest && corepack enable

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
RUN pnpm build

# --- Runtime stage ---
FROM node:26-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# Standalone build output bundles only the production runtime.
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

# Run as the built-in non-root node user.
USER node

EXPOSE 3000

CMD ["node", "server.js"]
