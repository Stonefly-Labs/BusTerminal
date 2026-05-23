variable "name" {
  description = <<-EOT
    Display name shown in the Entra portal and surfaced in federation-failure
    diagnostics (FR-030). Convention: `github-environment-<env>` for
    environment-scoped subjects; `github-branch-<branch>` for branch-scoped
    subjects.
  EOT
  type        = string

  validation {
    condition     = length(var.name) > 0 && length(var.name) <= 120
    error_message = "name must be 1-120 characters."
  }
}

variable "resource_group_name" {
  description = "Resource group of the parent managed identity. Required by the underlying provider even though the FIC itself is a child resource of the MI."
  type        = string
}

variable "parent_id" {
  description = <<-EOT
    Resource ID of the parent user-assigned managed identity that this
    credential federates *to*. Typically `module.workload_identity.id` or
    `module.pipeline_identity[<env>].resource_id`.
  EOT
  type        = string

  validation {
    condition     = can(regex("^/subscriptions/[0-9a-fA-F-]+/resourceGroups/[^/]+/providers/Microsoft.ManagedIdentity/userAssignedIdentities/[^/]+$", var.parent_id))
    error_message = "parent_id must be a fully-qualified user-assigned managed identity resource ID."
  }
}

variable "issuer" {
  description = <<-EOT
    OIDC issuer URL of the external identity provider. Defaults to
    GitHub Actions' issuer; override for other CI systems or external
    workloads. Must be HTTPS.
  EOT
  type        = string
  default     = "https://token.actions.githubusercontent.com"

  validation {
    condition     = can(regex("^https://", var.issuer))
    error_message = "issuer must be an HTTPS URL."
  }
}

variable "audience" {
  description = <<-EOT
    Federation audience value. Entra mandates `api://AzureADTokenExchange`
    for workload-identity federation and rejects all other values, so the
    default fits every current use case. Surfaced as a variable for
    forward-compatibility only.
  EOT
  type        = string
  default     = "api://AzureADTokenExchange"

  validation {
    condition     = length(var.audience) > 0
    error_message = "audience cannot be empty."
  }
}

variable "subject" {
  description = <<-EOT
    Federation subject pattern that the inbound OIDC token's `sub` claim
    must match exactly. Common shapes:
      - `repo:<org>/<repo>:environment:<env>`  (deployment env subjects)
      - `repo:<org>/<repo>:ref:refs/heads/<branch>`  (branch subjects)
      - `repo:<org>/<repo>:pull_request`  (PR subjects — avoid for prod)
    Per data-model.md § Federated Credential, the chosen subject MUST also
    appear verbatim in `docs/identity-and-secrets.md` so federation-drift
    failures can be diagnosed in one step.
  EOT
  type        = string

  validation {
    condition     = !can(regex("\\*", var.subject))
    error_message = "subject must not contain a wildcard (`*`). Overly broad subjects expand the trust surface beyond a single repo/branch/environment and require an ADR-recorded exception (data-model.md § Federated Credential)."
  }

  validation {
    condition     = length(var.subject) > 0
    error_message = "subject cannot be empty."
  }
}
