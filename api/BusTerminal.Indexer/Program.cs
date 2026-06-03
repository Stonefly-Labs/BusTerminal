// Spec 006 / Phase 1 T001 — minimal scaffold for the containerized Azure
// Functions indexer. Phase 2 T044 wires Cosmos + SearchClient + AzureCredentialFactory
// + OTel exporter against this builder. The current shape compiles cleanly under
// TreatWarningsAsErrors and registers the v2 native Functions runtime; no
// production logic is added in Phase 1.

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Cosmos change-feed trigger only — no HTTP surface, so no
// ConfigureFunctionsWebApplication call. Phase 2 T044 wires Cosmos client,
// SearchClient, IAzureCredentialFactory, mapper, poison handler, OTel exporter.

builder.Build().Run();
