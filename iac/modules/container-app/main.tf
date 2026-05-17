terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

locals {
  managed_identities = {
    system_assigned            = false
    user_assigned_resource_ids = [var.managed_identity_id]
  }

  registries = var.registry_login_server != null ? [{
    server   = var.registry_login_server
    identity = var.managed_identity_id
  }] : []

  secrets = {
    for secret_name, kv_secret_uri in var.key_vault_secrets : secret_name => {
      name                = secret_name
      key_vault_secret_id = kv_secret_uri
      identity            = var.managed_identity_id
    }
  }
}

module "app" {
  source  = "Azure/avm-res-app-containerapp/azurerm"
  version = "0.5.0"

  name                                  = var.name
  resource_group_name                   = var.resource_group_name
  container_app_environment_resource_id = var.container_apps_environment_id

  revision_mode      = "Single"
  managed_identities = local.managed_identities
  registries         = local.registries
  secrets            = length(local.secrets) > 0 ? local.secrets : null

  template = {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    containers = [{
      name   = var.name
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      env = concat(
        [for k, v in var.env_vars : { name = k, value = v }],
        [for k, secret_name in var.secret_env_vars : { name = k, secret_name = secret_name }]
      )

      liveness_probes = [{
        transport               = "HTTP"
        port                    = var.target_port
        path                    = "/healthz/live"
        timeout                 = 5
        interval_seconds        = 10
        initial_delay           = 5
        failure_count_threshold = 3
      }]

      readiness_probes = [{
        transport               = "HTTP"
        port                    = var.target_port
        path                    = "/healthz/ready"
        timeout                 = 5
        interval_seconds        = 10
        failure_count_threshold = 3
        success_count_threshold = 1
      }]

      startup_probe = [{
        transport               = "HTTP"
        port                    = var.target_port
        path                    = "/healthz/startup"
        timeout                 = 5
        interval_seconds        = 5
        failure_count_threshold = 30
      }]
    }]

    http_scale_rules = var.max_replicas > 0 ? [{
      name                = "http-concurrency"
      concurrent_requests = "50"
    }] : []
  }

  ingress = {
    external_enabled = var.ingress_external
    target_port      = var.target_port
    transport        = "auto"
    traffic_weight = [{
      latest_revision = true
      percentage      = 100
    }]
  }

  tags = var.tags
}
