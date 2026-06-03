# Spec 006 / Phase 1 T010 — variable surface (scaffold). Full schema lands in
# Phase 2 T015.

variable "name" {
  description = "Container App name."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group hosting the Container App."
  type        = string
}

variable "location" {
  description = "Azure region (typically inherited from the resource group)."
  type        = string
}

variable "container_apps_environment_id" {
  description = "Container Apps Environment resource id (from spec 005)."
  type        = string
}

variable "workload_uami_id" {
  description = "Workload user-assigned managed identity resource id (from spec 005)."
  type        = string
}

variable "workload_uami_client_id" {
  description = "Workload UAMI client id; injected to the container as `Cosmos__clientId`."
  type        = string
}

variable "container_image" {
  description = "Fully-qualified container image reference (registry/name:tag)."
  type        = string
}
