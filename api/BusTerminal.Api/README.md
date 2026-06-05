# BusTerminal.Api

The BusTerminal backend API. .NET 10, ASP.NET Core Minimal APIs, Vertical Slice Architecture, OpenAPI for every public surface.

## Registry walkthrough

For an end-to-end walkthrough of the **Service Bus registry** (CRUD endpoints, search proxy, audit retrieval, the Cosmos change-feed → Functions → AI Search indexing pipeline, dev-deploy steps) read:

- [`specs/006-service-bus-registry-core/quickstart.md`](../../specs/006-service-bus-registry-core/quickstart.md)

The registry vertical slices live under `Features/Registry/`. The shared registry infrastructure (Cosmos stores, AI Search adapter, audit store) lives under `Infrastructure/Persistence/` and `Infrastructure/Search/`.

## Local development

```bash
dotnet run --project BusTerminal.Api
```

The API listens on `https://localhost:5001` (HTTPS) by default. Authentication is Microsoft Identity Web JWT bearer; run `az login` once against the dev tenant — `DefaultAzureCredential` resolves your `AzureCliCredential` for Cosmos and AI Search calls.

## Project standards

The backend follows the standards in [`speckit-artifacts/tech-stack.md`](../../speckit-artifacts/tech-stack.md) §1 (Backend), §7 (Identity & Auth). Deviations require an ADR.
