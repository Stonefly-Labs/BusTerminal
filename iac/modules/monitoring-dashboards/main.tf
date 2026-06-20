terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

# Spec 009 / T115 — Application Insights workbook for discovery telemetry.
# Loaded as a single Azure Workbook ARM resource bound to the existing AI
# component. The workbook's panel definitions live in `discovery.json`
# (next to this file) so the data shape stays under version control and
# coding agents can read / edit it as a regular file.

resource "random_uuid" "workbook" {
  # A stable Workbook name is the resource's ID; we derive it from a
  # per-stack random UUID so multiple environments don't collide.
}

resource "azurerm_application_insights_workbook" "discovery" {
  name                = random_uuid.workbook.result
  resource_group_name = var.resource_group_name
  location            = var.location
  display_name        = var.display_name
  source_id           = lower(var.application_insights_id)
  category            = "workbook"
  description         = "Spec 009 discovery telemetry — runs/day, success rate, duration P50/P95, retries, failure-category breakdown. Bound to the BusTerminal.Discovery ActivitySource + Meter via custom-metric and customEvents queries."

  data_json = file("${path.module}/discovery.json")

  tags = var.tags
}
