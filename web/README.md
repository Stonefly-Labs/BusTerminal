# BusTerminal — Web App

The BusTerminal frontend. Next.js 16 (App Router), TypeScript strict, Tailwind v4, shadcn/ui (project-owned).

## Registry walkthrough

For an operator-level walkthrough of the **Service Bus registry** (manual registration, browse, search, edit, conflict resolution, delete, relationships, audit) — including local-dev and dev-deploy steps — read:

- [`specs/006-service-bus-registry-core/quickstart.md`](../specs/006-service-bus-registry-core/quickstart.md)

The registry routes live under `app/(authenticated)/registry/`. The shared registry data layer is at `lib/registry/`. The registry components are at `components/registry/`.

## Local development

```bash
pnpm install
pnpm dev
```

Open [http://localhost:3000](http://localhost:3000).

Auth signs in to the **real** BusTerminal dev tenant via MSAL (no mock provider). Run `az login` once against the dev tenant; the backend `DefaultAzureCredential` chain picks up your Azure CLI credential for Azure-SDK calls.

## Project standards

The frontend follows the standards in [`speckit-artifacts/tech-stack.md`](../speckit-artifacts/tech-stack.md) §2 (Frontend), §3 (Accessibility), §4 (Frontend Observability). Deviations require an ADR.
