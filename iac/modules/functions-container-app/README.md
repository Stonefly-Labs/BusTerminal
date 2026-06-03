# Functions container app module

Spec 006 / T015. Provisions a Container App that hosts the registry
indexer Functions image on the existing Container Apps Environment from
spec 005. Per [research §4](../../../specs/006-service-bus-registry-core/research.md)
this is the **v2 native Functions-on-CAE** hosting model — single
`azurerm_container_app` resource, no proxy `Microsoft.Web/sites` Function
App, no separate hidden container.

The container exposes no public ingress; its only execution surface is the
Cosmos change-feed trigger declared by
`api/BusTerminal.Indexer/Functions/RegistryEntityIndexer.cs`. CAE health
probes use the Functions runtime's internal `/healthz` endpoint.

Inputs bind the workload UAMI (spec 005) and inject the Cosmos / AI Search
endpoint environment variables documented in
[`indexer-events.md`](../../../specs/006-service-bus-registry-core/contracts/indexer-events.md).

Diagnostic settings attach via `iac/modules/diagnostic-settings` in the env
composition; this module does not provision them directly.

<!-- BEGIN_TF_DOCS -->
<!-- END_TF_DOCS -->
