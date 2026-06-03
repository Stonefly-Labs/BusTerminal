# Functions container app module

Spec 006 / Phase 1 T010 — module skeleton. Phase 2 T015 fills the body.

Provisions a Container App configured for the **v2 native Functions-on-Container-Apps**
hosting model (`kind="functionapp"` on `azurerm_container_app` — see
[research §4](../../../specs/006-service-bus-registry-core/research.md)). The
container exposes no public ingress; its only execution surface is the
Cosmos change-feed trigger.

Inputs bind the workload UAMI (spec 005) and inject the Cosmos / AI Search
endpoint environment variables documented in
[`indexer-events.md`](../../../specs/006-service-bus-registry-core/contracts/indexer-events.md).

Diagnostic settings attach via `iac/modules/diagnostic-settings` in the env
composition; this module does not provision them directly.

<!-- BEGIN_TF_DOCS -->
<!-- END_TF_DOCS -->
