# Placeholder versions.tf — Phase 1 scaffolding (T003). Phase 3 (T041) will
# replace this with the AVM + azurerm provider requirements. The minimal
# `required_version` block below satisfies tflint's terraform_required_version
# rule until then.
terraform {
  required_version = ">= 1.11.0"
}
