variable "name" {
  description = "Container App name."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Container App."
  type        = string
}

variable "container_apps_environment_id" {
  description = "Resource ID of the parent Container Apps Environment."
  type        = string
}

variable "managed_identity_id" {
  description = "Resource ID of the user-assigned managed identity used by the workload (for ACR pulls + Key Vault references)."
  type        = string
}

variable "image" {
  description = "Fully qualified container image reference (e.g., `acr.azurecr.io/busterminal/api:<sha>`)."
  type        = string
}

variable "registry_login_server" {
  description = "ACR login server hostname. Set to null to skip registry credential wiring (e.g., for public images)."
  type        = string
  default     = null
}

variable "target_port" {
  description = "Container TCP port the workload listens on."
  type        = number
}

variable "ingress_external" {
  description = "When true, the workload accepts traffic from outside the environment. Defaults to false — flip to true only for workloads that legitimately need public ingress."
  type        = bool
  default     = false
}

variable "min_replicas" {
  description = "Minimum replica count. Defaults to 0 (scale-to-zero)."
  type        = number
  default     = 0
}

variable "max_replicas" {
  description = "Maximum replica count."
  type        = number
  default     = 3
}

variable "cpu" {
  description = "CPU cores per replica."
  type        = number
  default     = 0.5
}

variable "memory" {
  description = "Memory per replica (e.g., `1Gi`)."
  type        = string
  default     = "1Gi"
}

variable "env_vars" {
  description = "Non-secret environment variables exposed to the container."
  type        = map(string)
  default     = {}
}

variable "secret_env_vars" {
  description = "Environment variables backed by Container Apps secrets. Key is the env-var name; value is the Container Apps secret name (which itself is mapped to a Key Vault secret via `key_vault_secrets`)."
  type        = map(string)
  default     = {}
}

variable "key_vault_secrets" {
  description = "Container Apps secrets backed by Key Vault secret URIs. Key is the secret name; value is the Key Vault secret versionless URI."
  type        = map(string)
  default     = {}
}

variable "tags" {
  description = "Tags applied to the Container App."
  type        = map(string)
  default     = {}
}
