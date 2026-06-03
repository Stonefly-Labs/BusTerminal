# Spec 006 / Phase 1 T010 — module skeleton.
# Phase 2 T015 will provision the Functions container app via azurerm_container_app
# with kind="functionapp" — the v2 native Functions-on-Container-Apps hosting
# model (research §4). The container has no public ingress (no inbound HTTP);
# the change-feed trigger is the only execution surface. Diagnostic settings
# attach via the existing iac/modules/diagnostic-settings wrapper in the env
# composition (T016).
