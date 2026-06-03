# Spec 006 / T015. Variables for the functions-container-app module.

variable "name" {
  description = "Container App name."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Container App."
  type        = string
}

variable "container_apps_environment_id" {
  description = "Container Apps Environment resource id (from spec 005)."
  type        = string
}

variable "workload_uami_id" {
  description = "Workload user-assigned managed identity resource id (from spec 005)."
  type        = string
}

variable "workload_uami_client_id" {
  description = "Workload UAMI client id. Injected as `AZURE_CLIENT_ID` and `Cosmos__clientId`."
  type        = string
}

variable "container_image" {
  description = "Fully-qualified container image reference (registry/name:tag)."
  type        = string
}

variable "registry_login_server" {
  description = "ACR login server (from spec 005)."
  type        = string
}

variable "cosmos_account_endpoint" {
  description = "Cosmos DB account endpoint URI (e.g., https://<acct>.documents.azure.com:443/)."
  type        = string
}

variable "cosmos_database_name" {
  description = "Cosmos database name (typically `canonical`)."
  type        = string
}

variable "cosmos_entities_container_name" {
  description = "Cosmos container name holding registry entities."
  type        = string
}

variable "cosmos_leases_container_name" {
  description = "Cosmos container name holding change-feed lease state."
  type        = string
}

variable "ai_search_endpoint" {
  description = "AI Search service endpoint URI."
  type        = string
}

variable "ai_search_index_name" {
  description = "AI Search index name (typically `registry-entities-v1`)."
  type        = string
}

variable "app_insights_connection_string_kv_secret_uri" {
  description = "Key Vault secret URI exposing the App Insights connection string. Mirrors the spec-005 hybrid AI ingestion pattern."
  type        = string
}

variable "min_replicas" {
  description = "Minimum replicas. The change-feed trigger keeps at least one warm so leases don't churn."
  type        = number
  default     = 1
}

variable "max_replicas" {
  description = "Maximum replicas. Single replica is sufficient for the spec-006 scale (research §3 + §16)."
  type        = number
  default     = 1
}

variable "cpu" {
  description = "vCPU per replica."
  type        = number
  default     = 0.5
}

variable "memory" {
  description = "Memory per replica (Gi)."
  type        = string
  default     = "1Gi"
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
