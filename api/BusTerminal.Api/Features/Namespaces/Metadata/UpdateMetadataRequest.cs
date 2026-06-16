using System.Text.Json;
using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Features.Namespaces.Metadata;

// Spec 008 / data-model.md §5 UpdateMetadataRequest +
// contracts/namespace-onboarding-api.yaml#/UpdateMetadataRequest. The Azure-
// identifier fields (azureResourceId, subscriptionId, resourceGroup, tenantId,
// region, namespaceName) are STRICTLY NOT permitted in the request body —
// validators reject when any of them is supplied. Raw JsonElement captures
// the wire-shape so we can detect prohibited keys.
public sealed record UpdateMetadataRequest(
    Guid Id,
    string DisplayName,
    string? Description,
    string? BusinessUnit,
    string? ProductOrApplication,
    string? CostCenter,
    string? Notes,
    IReadOnlyList<RegistryTag>? Tags,
    JsonElement? RawBody = null);
