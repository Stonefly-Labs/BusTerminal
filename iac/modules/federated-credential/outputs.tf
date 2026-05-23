output "credential_id" {
  description = "Resource ID of the federated identity credential. Useful for `depends_on` wiring in downstream resources that race FIC propagation."
  value       = azurerm_federated_identity_credential.this.id
}

output "name" {
  description = "Echoes the credential's display name for downstream references and documentation."
  value       = azurerm_federated_identity_credential.this.name
}

output "subject" {
  description = "Echoes the federation subject pattern. Surface this in plan output so PR reviewers can sanity-check the subject without reading the module call."
  value       = azurerm_federated_identity_credential.this.subject
}
