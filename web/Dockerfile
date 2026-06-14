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

# Next.js inlines `process.env.NEXT_PUBLIC_*` reads into the client
# bundle at `next build` time. Setting these at container runtime
# would be a no-op for the SPA. The CD workflow passes the env-
# specific values via `--build-arg`; we lift them to ENV here so
# `pnpm build` (= `next build`) sees them. Default to empty so the
# msal-config / api-client fallbacks fire cleanly when the image is
# built locally without args (mock-auth dev path).
ARG NEXT_PUBLIC_AZURE_AD_TENANT_ID=""
ARG NEXT_PUBLIC_AZURE_AD_CLIENT_ID=""
ARG NEXT_PUBLIC_API_BASE_URL=""
ARG NEXT_PUBLIC_API_SCOPE=""
ARG NEXT_PUBLIC_AUTH_MODE=""
ENV NEXT_PUBLIC_AZURE_AD_TENANT_ID=$NEXT_PUBLIC_AZURE_AD_TENANT_ID \
    NEXT_PUBLIC_AZURE_AD_CLIENT_ID=$NEXT_PUBLIC_AZURE_AD_CLIENT_ID \
    NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL \
    NEXT_PUBLIC_API_SCOPE=$NEXT_PUBLIC_API_SCOPE \
    NEXT_PUBLIC_AUTH_MODE=$NEXT_PUBLIC_AUTH_MODE

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
