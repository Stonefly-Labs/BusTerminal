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
