# probe-job-internal-caller

Opt-in Container Apps Job that proves SC-003 (a workload MI calling the
BusTerminal API and getting 200 on `/probe/read`). Spec 003 / US3.

Off by default — instantiate the module via the env-level `probe_job_enabled`
toggle when you need to prove the internal-caller path or after rotating the
workload MI's role assignments.

## What the job does

1. `az login --identity --client-id $AZURE_CLIENT_ID` — picks up the attached
   user-assigned managed identity.
2. `az account get-access-token --resource $API_SCOPE` — exchanges the MI
   for an Entra access token scoped to the BusTerminal API audience.
3. `curl /probe/read` with the bearer token.
4. Exit 0 on HTTP 200, non-zero otherwise. Prints status + response body
   to job logs for diagnostic visibility.

## Running the job

Once provisioned (after `tofu apply` with `probe_job_enabled = true`):

```sh
az containerapp job start \
  --name caj-bt-dev-probe-internal-caller \
  --resource-group rg-bt-dev

# Tail the logs:
az containerapp job logs show \
  --name caj-bt-dev-probe-internal-caller \
  --resource-group rg-bt-dev \
  --tail 100
```

A successful execution prints `[probe] OK — workload MI authenticated to
API as internal caller` and exits 0. A failure prints the actual status
code and response body so you can diagnose:

- `403` — workload MI lacks the required role on the API SP (check
  `assigned_api_app_roles` in the workload-identity module).
- `401` — token audience mismatch (check `api_scope` matches the backend's
  `AzureAd:Audience` setting).
- non-HTTP error — networking, DNS, or `az login --identity` failure.

## Usage

```hcl
module "probe_job_internal_caller" {
  count  = var.probe_job_enabled ? 1 : 0
  source = "../../modules/probe-job-internal-caller"

  name                        = "caj-bt-dev-probe-internal-caller"
  resource_group_name         = azurerm_resource_group.this.name
  location                    = azurerm_resource_group.this.location
  container_apps_environment_id = module.container_apps_env.id
  managed_identity_id         = module.workload_identity.id
  workload_identity_client_id = module.workload_identity.client_id
  api_url                     = "https://${module.backend_app.fqdn_url}"
  api_scope                   = "api://${var.entra_api_client_id}/.default"

  tags = local.shared_tags
}
```

## Why a separate module

The job is operationally distinct from the long-running Container Apps
(`backend_app` / `frontend_app`) — it runs to completion, costs nothing
when idle, and exists purely to validate the auth path. Bundling it into
the env composition directly would clutter `main.tf` with a debug-only
artifact and make it harder to disable cleanly. The `count =
var.probe_job_enabled ? 1 : 0` pattern keeps state empty when disabled.
