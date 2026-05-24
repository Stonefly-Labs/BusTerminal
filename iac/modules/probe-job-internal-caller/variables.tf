variable "name" {
  description = "Container Apps Job name. Convention: `caj-bt-<env>-probe-internal-caller`."
  type        = string

  validation {
    condition     = can(regex("^caj-bt-(dev|test|prod)-[a-z0-9-]+$", var.name))
    error_message = "name must match `caj-bt-(dev|test|prod)-<purpose>`."
  }
}

variable "resource_group_name" {
  description = "Resource group holding the job."
  type        = string
}

variable "location" {
  description = "Azure region for the job."
  type        = string
}

variable "container_apps_environment_id" {
  description = "Resource ID of the Container Apps Environment hosting the job. Shared with the BusTerminal API for proximity (cheaper, faster)."
  type        = string
}

variable "managed_identity_id" {
  description = "Resource ID of the user-assigned managed identity the job runs under. Must be the SAME MI that holds the `BusTerminal.*` app-role assignment being exercised by the probe."
  type        = string
}

variable "workload_identity_client_id" {
  description = "Client ID (applicationId) of the user-assigned managed identity. Passed to `az login --identity --client-id $AZURE_CLIENT_ID` so the Azure CLI selects the right MI when multiple are attached."
  type        = string
}

variable "api_url" {
  description = "Base URL of the BusTerminal API (e.g. `https://ca-bt-dev-api.<env>.azurecontainerapps.io`). The probe appends `/probe/read`."
  type        = string

  validation {
    condition     = can(regex("^https://", var.api_url))
    error_message = "api_url must be an HTTPS URL (FR-031)."
  }
}

variable "api_scope" {
  description = "Entra scope the workload MI requests a token for (e.g. `api://<api-app-id>/.default`). Must match the API audience the backend validates."
  type        = string

  validation {
    condition     = can(regex("^api://", var.api_scope))
    error_message = "api_scope must start with `api://`."
  }
}

variable "probe_image" {
  description = "Container image carrying `az`, `curl`, and a POSIX shell. Defaults to the official Microsoft Azure CLI image."
  type        = string
  default     = "mcr.microsoft.com/azure-cli:latest"
}

variable "tags" {
  description = "Tags applied to the job."
  type        = map(string)
  default     = {}
}
