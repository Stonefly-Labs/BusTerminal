terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

# Internal-caller probe Container Apps Job (spec 003 / US3 / SC-003).
#
# A manually-triggered, single-replica job that:
#
#   1. `az login --identity --client-id $AZURE_CLIENT_ID` — picks up the
#      attached user-assigned managed identity.
#   2. `az account get-access-token --resource $API_SCOPE` — exchanges the MI
#      for an Entra access token scoped to the BusTerminal API audience.
#   3. `curl /probe/read` with the token — exercises the API authorization
#      pipeline identically to a human caller (FR-012, no internal-trust
#      bypass).
#   4. Exits 0 on HTTP 200, non-zero otherwise.
#
# Intended as a re-runnable SC-003 smoke. The job is off by default at the
# env composition level (`var.probe_job_enabled = false`) — instantiate it
# explicitly when proving an internal-caller path or after rotating the
# workload MI's role assignments.
#
# Image: `mcr.microsoft.com/azure-cli:latest` is the official Microsoft
# image carrying `az`, `curl`, and `jq`. No custom build required; the
# probe is a shell script encoded in `args`.

locals {
  # Single-line script kept inside `args` so the resource is fully
  # declarative — no separate file to mount, no ConfigMap. The `set -euo
  # pipefail` ensures any non-zero step bubbles up as a job failure.
  probe_script = <<-EOT
    set -euo pipefail
    echo "[probe] az login --identity client_id=$AZURE_CLIENT_ID"
    az login --identity --client-id "$AZURE_CLIENT_ID" > /dev/null
    echo "[probe] acquiring token for resource=$API_SCOPE"
    TOKEN=$(az account get-access-token --resource "$API_SCOPE" --query accessToken -o tsv)
    echo "[probe] calling GET $API_URL/probe/read"
    BODY=$(mktemp)
    STATUS=$(curl -sS -o "$BODY" -w '%%{http_code}' -H "Authorization: Bearer $TOKEN" "$API_URL/probe/read")
    echo "[probe] response status: $STATUS"
    echo "[probe] response body:"
    cat "$BODY"
    echo ""
    if [ "$STATUS" = "200" ]; then
      echo "[probe] OK — workload MI authenticated to API as internal caller"
      exit 0
    else
      echo "[probe] FAIL — expected 200, got $STATUS"
      exit 1
    fi
  EOT
}

resource "azurerm_container_app_job" "this" {
  name                         = var.name
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_apps_environment_id
  tags                         = var.tags

  # 5-minute replica timeout is generous for a curl + az get-token round-trip;
  # the actual call typically completes in < 10s. No retry — a failure here
  # is a configuration issue, not a transient, so we want the failure visible
  # rather than masked by automatic retries.
  replica_timeout_in_seconds = 300
  replica_retry_limit        = 0

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name    = "probe"
      image   = var.probe_image
      cpu     = 0.25
      memory  = "0.5Gi"
      command = ["/bin/sh", "-c"]
      args    = [local.probe_script]

      env {
        name  = "AZURE_CLIENT_ID"
        value = var.workload_identity_client_id
      }

      env {
        name  = "API_SCOPE"
        value = var.api_scope
      }

      env {
        name  = "API_URL"
        value = var.api_url
      }
    }
  }
}
