variable "resource_group_name" {
  description = "Resource group hosting the Application Insights workbook."
  type        = string
}

variable "location" {
  description = "Azure region for the workbook resource."
  type        = string
}

variable "application_insights_id" {
  description = "Resource ID of the Application Insights component the workbook targets. Source ID lower-cases per ARM convention."
  type        = string
}

variable "display_name" {
  description = "Display name shown in the Azure portal."
  type        = string
  default     = "BusTerminal — Discovery telemetry"
}

variable "tags" {
  description = "Tags applied to the workbook resource."
  type        = map(string)
  default     = {}
}
