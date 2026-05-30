# Container App module

Wraps the Azure Verified Module
[`Azure/avm-res-app-containerapp/azurerm` v0.5.0](https://registry.terraform.io/modules/Azure/avm-res-app-containerapp/azurerm/0.5.0)
to provision a long-running Container App with:

- User-assigned managed identity bound for ACR pulls and Key Vault secret
  references (FR-015 — no inline secrets, no admin keys)
- Optional `registries` block that wires the workload UAMI to ACR by
  resource ID, not by registry credentials
- `key_vault_secrets` map that materializes app-level secrets as Key Vault
  references resolved with the workload UAMI at startup

Spec 002 / Spec 005 — primary deployment surface for the BusTerminal
backend + frontend container apps.

<!-- BEGIN_TF_DOCS -->


## Inputs

| Name | Description | Type | Default | Required |
| ---- | ----------- | ---- | ------- | :------: |
| <a name="input_container_apps_environment_id"></a> [container\_apps\_environment\_id](#input\_container\_apps\_environment\_id) | Resource ID of the parent Container Apps Environment. | `string` | n/a | yes |
| <a name="input_image"></a> [image](#input\_image) | Fully qualified container image reference (e.g., `acr.azurecr.io/busterminal/api:<sha>`). | `string` | n/a | yes |
| <a name="input_managed_identity_id"></a> [managed\_identity\_id](#input\_managed\_identity\_id) | Resource ID of the user-assigned managed identity used by the workload (for ACR pulls + Key Vault references). | `string` | n/a | yes |
| <a name="input_name"></a> [name](#input\_name) | Container App name. | `string` | n/a | yes |
| <a name="input_resource_group_name"></a> [resource\_group\_name](#input\_resource\_group\_name) | Resource group hosting the Container App. | `string` | n/a | yes |
| <a name="input_target_port"></a> [target\_port](#input\_target\_port) | Container TCP port the workload listens on. | `number` | n/a | yes |
| <a name="input_cpu"></a> [cpu](#input\_cpu) | CPU cores per replica. | `number` | `0.5` | no |
| <a name="input_env_vars"></a> [env\_vars](#input\_env\_vars) | Non-secret environment variables exposed to the container. | `map(string)` | `{}` | no |
| <a name="input_ingress_external"></a> [ingress\_external](#input\_ingress\_external) | When true, the workload accepts traffic from outside the environment. Defaults to false — flip to true only for workloads that legitimately need public ingress. | `bool` | `false` | no |
| <a name="input_key_vault_secrets"></a> [key\_vault\_secrets](#input\_key\_vault\_secrets) | Container Apps secrets backed by Key Vault secret URIs. Key is the secret name; value is the Key Vault secret versionless URI. | `map(string)` | `{}` | no |
| <a name="input_max_replicas"></a> [max\_replicas](#input\_max\_replicas) | Maximum replica count. | `number` | `3` | no |
| <a name="input_memory"></a> [memory](#input\_memory) | Memory per replica (e.g., `1Gi`). | `string` | `"1Gi"` | no |
| <a name="input_min_replicas"></a> [min\_replicas](#input\_min\_replicas) | Minimum replica count. Defaults to 0 (scale-to-zero). | `number` | `0` | no |
| <a name="input_registry_login_server"></a> [registry\_login\_server](#input\_registry\_login\_server) | ACR login server hostname. Set to null to skip registry credential wiring (e.g., for public images). | `string` | `null` | no |
| <a name="input_secret_env_vars"></a> [secret\_env\_vars](#input\_secret\_env\_vars) | Environment variables backed by Container Apps secrets. Key is the env-var name; value is the Container Apps secret name (which itself is mapped to a Key Vault secret via `key_vault_secrets`). | `map(string)` | `{}` | no |
| <a name="input_tags"></a> [tags](#input\_tags) | Tags applied to the Container App. | `map(string)` | `{}` | no |

## Outputs

| Name | Description |
| ---- | ----------- |
| <a name="output_fqdn_url"></a> [fqdn\_url](#output\_fqdn\_url) | HTTPS URL of the workload's ingress (empty string when ingress is disabled). |
| <a name="output_id"></a> [id](#output\_id) | Resource ID of the Container App. |
| <a name="output_ingress_external"></a> [ingress\_external](#output\_ingress\_external) | Whether the workload accepts external traffic. Echoed for downstream policy assertions. |
| <a name="output_name"></a> [name](#output\_name) | Container App name. |
<!-- END_TF_DOCS -->
