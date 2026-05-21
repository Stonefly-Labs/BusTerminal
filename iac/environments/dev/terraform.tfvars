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

# Spec 003 — stable role_id GUIDs for the four BusTerminal platform app roles
# on the API app registration. These are not secrets (Entra exposes them in
# token role claims indirectly via assignments and via the directory). They
# MUST stay constant once assignments exist — changing them orphans the
# assignments. Generated 2026-05-20.
platform_role_ids = {
  admin     = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a01"
  operator  = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a02"
  reader    = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a03"
  developer = "9c1f0c4d-3a4b-4c5e-9f01-72fcb8b51a04"
}
