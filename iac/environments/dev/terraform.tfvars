# Non-secret defaults for the dev environment.
#
# Sensitive or per-tenant values (subscription_id, entra_tenant_id, entra
# client IDs, image references, unique_suffix, github_org_repo) are supplied
# by the pipeline via `-var` flags or environment variables — NEVER committed.
#
# This file holds only values that are safe to publish.

environment_name = "dev"
location         = "eastus2"
naming_prefix    = "bt-dev"

frontend_min_replicas = 0
frontend_max_replicas = 3
backend_min_replicas  = 0
backend_max_replicas  = 3

tags = {
  slice = "002-solution-foundation"
}

# Spec 003 (admin/operator/reader/developer) — stable role_id GUIDs for the
# BusTerminal platform app roles on the API app registration. These are not
# secrets (Entra exposes them in token role claims indirectly via assignments
# and via the directory). They MUST stay constant once assignments exist —
# changing them orphans the assignments. Generated 2026-05-20.
#
# Spec 008 (namespace_administrator) — pinned 2026-06-14 per
# `specs/008-namespace-onboarding/contracts/outputs-contract.md §1.1` and
# tasks.md T005. Extends the existing `9c1f0c4d-...a0X` series with `…a05`.
platform_role_ids = {
  admin                   = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a01"
  operator                = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a02"
  reader                  = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a03"
  developer               = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a04"
  namespace_administrator = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a05"
}

# Spec 004 — canonical Cosmos SQL database name. Safe to publish (logical name,
# not a secret). Defaults match the cosmos-canonical-store module defaults; the
# explicit value here documents intent and gives the env a single edit point.
canonical_db_name = "busterminal-canonical"

# Spec 005 — Infrastructure Baseline (T065)
# Per `specs/005-infrastructure-baseline/contracts/config-profile-schema.md` and
# the Q2c networking clarification. Dev opts into public access on data
# services AND provisions warm PEs — the destructive flip (public access off
# on data services) is a future spec, not this slice. See spec.md §Clarifications
# for the Q2c trade-off rationale.
network_address_space         = ["10.50.0.0/16"]
subnet_integration_cidr       = "10.50.0.0/23"
subnet_private_endpoints_cidr = "10.50.2.0/24"

data_services_public_access_enabled = true
private_endpoints_enabled           = true

ai_search_sku = "basic"

# AI Search regional override — eastus2 was out of basic-SKU search
# capacity on 2026-05-31 (`InsufficientResourcesAvailable`). westus3
# accepted the provisioning request on probe. Flip back to null (or
# remove the line) to colocate with the env once eastus2 has capacity.
ai_search_location = "westus3"

# Dev cost control (2026-07-01) — the original Premium registry (with the
# AVM's default zone redundancy) cost ~$50/mo for features dev doesn't use.
# NOTE: the Premium→Basic + ZR-off flip REPLACED the registry (zoneRedundancy
# is immutable): images were wiped and rebuilt by the next cd-dev run.
# Basic's 10 GiB fills over time from per-deploy :sha tags — when near the
# limit, purge old manifests (az acr manifest list-metadata / delete) or bump
# to Standard (in-place upgrade).
acr_sku                     = "Basic"
acr_zone_redundancy_enabled = false

service_bus_sku = "Standard"
# service_bus_capacity is intentionally omitted — required only when sku=Premium.

# Dev's KV was provisioned with purge protection ENABLED (the module hardcoded
# `true` pre-spec-005). Azure does not permit disabling purge protection once
# enabled, so dev's tfvars stays `true` to match the deployed reality. The
# variable default in variables.tf remains `false` per the spec's per-env
# table — a NEW dev environment that's never opted in could keep that default.
key_vault_purge_protection_enabled   = true
key_vault_soft_delete_retention_days = 90

log_analytics_retention_days = 30

# Spec 005 / T134 / FR-010 — backend Container App ingress posture. Dev keeps
# external ingress on so the existing developer + smoke-test workflows (hitting
# the backend over the public internet via the FQDN output) continue to work.
# Prod template defaults to false.
backend_external_ingress = true
