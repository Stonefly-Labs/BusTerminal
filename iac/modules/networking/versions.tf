# Placeholder versions.tf — Phase 1 scaffolding (T002). Phase 3 (T033) will
# replace this with the AVM + azurerm provider requirements (plus the spec 005
# new provider trio: random, azapi, modtm). The minimal `required_version`
# block below satisfies tflint's terraform_required_version rule until then.
terraform {
  required_version = ">= 1.11.0"
}
