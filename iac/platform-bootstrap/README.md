# Platform Bootstrap Module

One-time OpenTofu module that creates the resources required before any
environment-scoped composition can run:

- Resource group `rg-busterminal-tfstate`
- Azure Storage Account holding the OpenTofu state files (versioning + soft delete + HTTPS-only + TLS 1.2 minimum)
- Blob container `tfstate`
- Per-environment user-assigned managed identities for the GitHub Actions pipeline
- Federated identity credentials on each identity bound to `repo:<org>/BusTerminal:environment:<env>`
- RBAC role assignments granting each pipeline identity `Storage Blob Data Contributor` on the state account

This module's **own** state lives locally and is not committed (see `.gitignore` rule covering `*.tfstate*`).

## Usage

```sh
cd iac/platform-bootstrap
tofu init -backend=false

tofu apply \
  -var subscription_id=<your-subscription-id> \
  -var github_org_repo=<org>/BusTerminal \
  -var tfstate_storage_account_name=<globally-unique-name> \
  -var 'environments=["dev"]'
```

When you later add `test` or `prod`, re-run with the additional names in the `environments` set.

After apply, capture the outputs and configure them in GitHub:

| Output | GitHub destination |
|--------|--------------------|
| `azure_tenant_id` | Repository variable `AZURE_TENANT_ID` |
| `azure_subscription_id` | Repository variable `AZURE_SUBSCRIPTION_ID` |
| `azure_client_ids["<env>"]` | Deployment environment `<env>` → variable `AZURE_CLIENT_ID` |
| `tfstate_resource_group` | Repository variable `TFSTATE_RESOURCE_GROUP` |
| `tfstate_storage_account_name` | Repository variable `TFSTATE_STORAGE_ACCOUNT_NAME` |
| `tfstate_container_name` | Repository variable `TFSTATE_CONTAINER_NAME` |

## Manual fallback (FR-082b)

If you prefer not to run OpenTofu against your subscription, the manual `az` CLI
walkthrough is provided in [`scripts/bootstrap-platform-manual.md`](../../scripts/bootstrap-platform-manual.md).
The end state is identical to running this module — every resource the module
creates is also documented as a one-liner in that walkthrough.
