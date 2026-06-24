# Spec 006 / T015 / research §4. v2 native Azure Functions on Azure Container
# Apps: a single `Microsoft.App/containerApps` resource with the envelope
# property `kind = "functionapp"`. This is the recommended native model
# (per learn.microsoft.com/azure/container-apps/migrate-functions) — it gives
# the function host KEDA event-driven scaling, scale-to-zero, and revision
# management instead of running as a plain container app.
#
# Why azapi and not azurerm: `azurerm_container_app` (v4) does NOT expose a
# `kind` argument — azurerm's only Function App resources are the App Service
# plan model (azurerm_linux_function_app et al.), which is a different hosting
# stack. The `kind=functionapp` envelope field on Microsoft.App/containerApps
# is only reachable via azapi. (versions.tf T010 anticipated this fallback.)
# `kind` is immutable, so converting the existing plain container app to this
# resource is a replace — safe here: the indexer is a stateless worker whose
# only durable state is the Cosmos change-feed lease checkpoint + tombstones.
#
# No public ingress — the Cosmos change-feed + the spec-009 Service Bus trigger
# are the only execution surfaces; there is no inbound HTTP.
#
# The workload UAMI is bound directly; the indexer uses it for:
#   - AAD against Cosmos (Cosmos__credential = managedidentity)
#   - AAD against AI Search (DefaultAzureCredential picks up AZURE_CLIENT_ID)
#   - AAD against the discovery Service Bus queue (spec 009)
#   - Pulling the indexer image from ACR (registry binding via identity)
#
# App Insights connection string is mounted as a Container Apps secret backed
# by Key Vault (mirrors the spec-005 container-app module's pattern). All
# other env vars are plain values; no secrets in this module.

resource "azapi_resource" "indexer" {
  type      = "Microsoft.App/containerApps@2024-10-02-preview"
  name      = var.name
  parent_id = var.resource_group_id
  location  = var.location
  tags      = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.workload_uami_id]
  }

  body = {
    # `kind` is the single envelope field that switches this Container App into
    # the native Azure Functions hosting model (the v2 model). It lives inside
    # `body` (the ARM resource envelope), not as a top-level azapi argument.
    kind = "functionapp"

    properties = {
      environmentId = var.container_apps_environment_id

      configuration = {
        activeRevisionsMode = "Single"

        registries = [
          {
            server   = var.registry_login_server
            identity = var.workload_uami_id
          },
        ]

        secrets = [
          {
            name        = "applicationinsights-connection-string"
            keyVaultUrl = var.app_insights_connection_string_kv_secret_uri
            identity    = var.workload_uami_id
          },
        ]
      }

      template = {
        scale = {
          minReplicas = var.min_replicas
          maxReplicas = var.max_replicas
        }

        containers = [
          {
            name  = var.name
            image = var.container_image
            resources = {
              cpu    = var.cpu
              memory = var.memory
            }
            env = [
              { name = "AZURE_CLIENT_ID", value = var.workload_uami_client_id },
              { name = "Cosmos__accountEndpoint", value = var.cosmos_account_endpoint },
              { name = "Cosmos__credential", value = "managedidentity" },
              { name = "Cosmos__clientId", value = var.workload_uami_client_id },
              { name = "COSMOS_DATABASE_NAME", value = var.cosmos_database_name },
              { name = "COSMOS_REGISTRY_ENTITIES_CONTAINER", value = var.cosmos_entities_container_name },
              { name = "COSMOS_REGISTRY_LEASES_CONTAINER", value = var.cosmos_leases_container_name },
              { name = "AI_SEARCH_ENDPOINT", value = var.ai_search_endpoint },
              { name = "AI_SEARCH_INDEX_NAME", value = var.ai_search_index_name },
              # App Insights connection string via the Container Apps secret above.
              { name = "APPLICATIONINSIGHTS_CONNECTION_STRING", secretRef = "applicationinsights-connection-string" },
              { name = "OTEL_SERVICE_NAME", value = "busterminal-indexer" },
              { name = "FUNCTIONS_WORKER_RUNTIME", value = "dotnet-isolated" },

              # Spec 009 — discovery worker Service Bus trigger. DiscoveryRequested
              # binds [ServiceBusTrigger("%Discovery:ServiceBus:QueueName%",
              # Connection = "ServiceBus")]. Without BOTH the function fails
              # indexing ("does not resolve to a value") and the host disables it,
              # so the discovery-requested queue is never drained.
              { name = "ServiceBus__fullyQualifiedNamespace", value = var.service_bus_fqdn },
              { name = "Discovery__ServiceBus__QueueName", value = var.discovery_queue_name },

              # AzureWebJobsStorage — AAD-only. The Functions runtime expects this
              # at startup even though the triggers (Cosmos change-feed + Service
              # Bus) don't use it. No connection strings / shared keys — the
              # workload UAMI holds Storage Blob Data Owner on the account.
              { name = "AzureWebJobsStorage__accountName", value = var.azure_webjobs_storage_account_name },
              { name = "AzureWebJobsStorage__credential", value = "managedidentity" },
              { name = "AzureWebJobsStorage__clientId", value = var.workload_uami_client_id },
            ]
          },
        ]
      }
    }
  }

  # The functionapp kind + platform-managed fields are newer than the bundled
  # azapi schema; skip client-side validation so the apply isn't rejected.
  schema_validation_enabled = false

  # Surface the (optional) ingress fqdn for the output; the indexer configures
  # no ingress, so this resolves to null and the output falls back to "".
  response_export_values = ["properties.configuration.ingress.fqdn"]

  # The indexer Container App is recoverable from image + IaC; the Cosmos
  # change-feed lease checkpoint + tombstone markers mean replace is
  # non-destructive in practice. prevent_destroy is intentionally OFF.
}
