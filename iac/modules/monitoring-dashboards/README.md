# monitoring-dashboards

Spec 009 / T115 — Azure Workbook for discovery telemetry, defined as IaC.

## What this module provisions

- One `azurerm_application_insights_workbook` (the discovery workbook) bound
  to the existing Application Insights component passed in via
  `application_insights_id`.

The workbook contents (panels, KQL queries, parameters) live in
[`discovery.json`](./discovery.json) next to this file. The panel set:

| Panel | Source | Notes |
|---|---|---|
| Runs / day | `customEvents` `discovery.run.completed` | Grouped by status (Succeeded / Failed). |
| Success rate | `customEvents` `discovery.run.completed` | Hourly success-ratio time series. |
| Duration P50 / P95 / P99 | `customMetrics` `discovery.run.duration` | Time series in seconds. |
| Retry counts | `customMetrics` `discovery.arm.retries` | Stacked by phase (FetchQueues, FetchTopics, …). |
| Failure category | `customEvents` `discovery.run.completed` filtered `Failed` | Pie chart by `DiscoveryFailureCategory`. |

A workbook-level `NamespaceId` parameter narrows every panel to a single
registered namespace; leave blank to see all.

## Usage

```hcl
module "discovery_dashboard" {
  source = "../../modules/monitoring-dashboards"

  resource_group_name     = azurerm_resource_group.this.name
  location                = azurerm_resource_group.this.location
  application_insights_id = module.monitoring.application_insights_id

  display_name = "BusTerminal — Discovery telemetry (dev)"

  tags = local.shared_tags
}
```

## Inputs

| Name | Type | Required | Description |
|---|---|---|---|
| `resource_group_name` | `string` | ✅ | RG hosting the workbook resource. |
| `location` | `string` | ✅ | Azure region. |
| `application_insights_id` | `string` | ✅ | AI component the workbook targets. Lower-cased before use per ARM convention. |
| `display_name` | `string` | ❌ | Portal label (default "BusTerminal — Discovery telemetry"). |
| `tags` | `map(string)` | ❌ | Tags applied to the workbook. |

## Outputs

| Name | Description |
|---|---|
| `workbook_id` | Resource ID of the workbook. |
| `workbook_name` | GUID-shaped name. |

## Editing the panels

`discovery.json` is the source of truth. To preview edits without applying
the Tofu plan:

1. Open the workbook in the Azure portal.
2. Switch to "Advanced editor" → "Gallery template (ARM)" view.
3. Paste the JSON, iterate, then save the new JSON back to this file.

The workbook intentionally does NOT auto-load from a portal export — every
panel review is captured in code review.

<!-- BEGIN_TF_DOCS -->
## Requirements

| Name | Version |
| ---- | ------- |
| <a name="requirement_terraform"></a> [terraform](#requirement\_terraform) | >= 1.11.0 |
| <a name="requirement_azurerm"></a> [azurerm](#requirement\_azurerm) | ~> 4.0 |
| <a name="requirement_random"></a> [random](#requirement\_random) | ~> 3.6 |

## Providers

| Name | Version |
| ---- | ------- |
| <a name="provider_azurerm"></a> [azurerm](#provider\_azurerm) | ~> 4.0 |
| <a name="provider_random"></a> [random](#provider\_random) | ~> 3.6 |

## Modules

No modules.

## Resources

| Name | Type |
| ---- | ---- |
| [azurerm_application_insights_workbook.discovery](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/application_insights_workbook) | resource |
| [random_uuid.workbook](https://registry.terraform.io/providers/hashicorp/random/latest/docs/resources/uuid) | resource |

## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_application_insights_id"></a> [application\_insights\_id](#input\_application\_insights\_id) | Resource ID of the Application Insights component the workbook targets. Source ID lower-cases per ARM convention. | `string` | n/a | yes |
| <a name="input_display_name"></a> [display\_name](#input\_display\_name) | Display name shown in the Azure portal. | `string` | `"BusTerminal — Discovery telemetry"` | no |
| <a name="input_location"></a> [location](#input\_location) | Azure region for the workbook resource. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the Application Insights workbook. | `string` | n/a | yes |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the workbook resource. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_workbook_id"></a> [workbook\_id](#output\_workbook\_id) | Resource ID of the discovery workbook. |
| <a name="output_workbook_name"></a> [workbook\_name](#output\_workbook\_name) | GUID-shaped name of the workbook (consumes as workbook id in the Azure portal URL). |
<!-- END_TF_DOCS -->