# Spec 006 / Phase 1 T009 — module skeleton.
# Phase 2 T014 will provision the index via azapi_resource using the
# Microsoft.Search/searchServices/indexes@2024-07-01 ARM envelope and
# reading the body from var.index_definition_path (defaults to the spec-006
# contract file). See research §5 for the azurerm-vs-azapi rationale.
