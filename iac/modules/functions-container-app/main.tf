# Spec 006 / T015 / research §4. v2 native Azure Functions on Azure Container
# Apps: a single `azurerm_container_app` resource with `kind = "functionapp"`.
# No public ingress — the change-feed trigger is the only execution surface;
# the CAE handles internal health probing on the runtime's default `/healthz`
# endpoint.
#
# The workload UAMI is bound directly; the indexer uses it for:
#   - AAD against Cosmos (Cosmos__credential = managedidentity)
#   - AAD against AI Search (DefaultAzureCredential picks up AZURE_CLIENT_ID)
#   - Pulling the indexer image from ACR (registry binding via identity)
#
# App Insights connection string is mounted as a Container Apps secret backed
# by Key Vault (mirrors the spec-005 container-app module's pattern). All
# other env vars are plain values; no secrets in this module.

resource "azurerm_container_app" "indexer" {
  name                         = var.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_apps_environment_id
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [var.workload_uami_id]
  }

  registry {
    server   = var.registry_login_server
    identity = var.workload_uami_id
  }

  secret {
    name                = "applicationinsights-connection-string"
    key_vault_secret_id = var.app_insights_connection_string_kv_secret_uri
    identity            = var.workload_uami_id
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = var.name
      image  = var.container_image
      cpu    = var.cpu
      memory = var.memory

      env {
        name  = "AZURE_CLIENT_ID"
        value = var.workload_uami_client_id
      }
      env {
        name  = "Cosmos__accountEndpoint"
        value = var.cosmos_account_endpoint
      }
      env {
        name  = "Cosmos__credential"
        value = "managedidentity"
      }
      env {
        name  = "Cosmos__clientId"
        value = var.workload_uami_client_id
      }
      env {
        name  = "COSMOS_DATABASE_NAME"
        value = var.cosmos_database_name
      }
      env {
        name  = "COSMOS_REGISTRY_ENTITIES_CONTAINER"
        value = var.cosmos_entities_container_name
      }
      env {
        name  = "COSMOS_REGISTRY_LEASES_CONTAINER"
        value = var.cosmos_leases_container_name
      }
      env {
        name  = "AI_SEARCH_ENDPOINT"
        value = var.ai_search_endpoint
      }
      env {
        name  = "AI_SEARCH_INDEX_NAME"
        value = var.ai_search_index_name
      }
      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "applicationinsights-connection-string"
      }
      env {
        name  = "OTEL_SERVICE_NAME"
        value = "busterminal-indexer"
      }
      env {
        name  = "FUNCTIONS_WORKER_RUNTIME"
        value = "dotnet-isolated"
      }

      # Spec 009 — discovery worker Service Bus trigger. The DiscoveryRequested
      # function binds `[ServiceBusTrigger("%Discovery:ServiceBus:QueueName%",
      # Connection = "ServiceBus")]`: the queue name resolves from
      # `Discovery:ServiceBus:QueueName` and the AAD connection from
      # `ServiceBus__fullyQualifiedNamespace`. Without BOTH the function fails
      # indexing ("does not resolve to a value") and the host disables it, so
      # the discovery-requested queue is never drained.
      env {
        name  = "ServiceBus__fullyQualifiedNamespace"
        value = var.service_bus_fqdn
      }
      env {
        name  = "Discovery__ServiceBus__QueueName"
        value = var.discovery_queue_name
      }

      # AzureWebJobsStorage — AAD-only. The Functions runtime expects
      # this connection at startup even when the only trigger (Cosmos
      # change-feed) doesn't need it. Without it the runtime reports
      # the host as unhealthy and the indexer container logs spam
      # "Unable to create client for AzureWebJobsStorage" every 30s.
      # No connection strings, no shared keys — the workload UAMI is
      # granted Storage Blob Data Owner on the account by the env
      # composition.
      env {
        name  = "AzureWebJobsStorage__accountName"
        value = var.azure_webjobs_storage_account_name
      }
      env {
        name  = "AzureWebJobsStorage__credential"
        value = "managedidentity"
      }
      env {
        name  = "AzureWebJobsStorage__clientId"
        value = var.workload_uami_client_id
      }
    }
  }

  tags = var.tags

  # The indexer Container App is recoverable from image + IaC; tombstone
  # markers in the Cosmos store and the change-feed lease checkpoint mean
  # replace is non-destructive in practice. prevent_destroy is intentionally
  # OFF — env-level rebuilds should be cheap.
}
