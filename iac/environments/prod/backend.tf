# Remote state backend for the `prod` environment.
#
# Static configuration here pins:
#   - key:              fixed per-environment partition inside the bootstrap container
#   - use_oidc:         pipeline authenticates via GitHub OIDC federation (FR-043)
#   - use_azuread_auth: state operations use Entra-issued tokens, not storage keys
#
# Per-deploy dynamic configuration is supplied by the pipeline via
# `-backend-config=` flags (see `quickstart.md` §B.4):
#   - resource_group_name
#   - storage_account_name
#   - container_name
#   - subscription_id
#   - tenant_id
#   - client_id  (the prod pipeline managed identity from platform-bootstrap)
#
# Local developer workflows MAY initialize this with `-backend=false` to keep
# state local; remote state is required for shared / CI usage (FR-082).
terraform {
  backend "azurerm" {
    key              = "envs/prod/terraform.tfstate"
    use_oidc         = true
    use_azuread_auth = true
  }
}
