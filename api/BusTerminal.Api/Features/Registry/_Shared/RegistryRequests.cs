using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / contracts/registry-api.yaml. Request DTOs shared across the
// per-entity-type create/update endpoints. The shape mirrors RegistryEntity
// (per the schema) minus the server-managed fields the API stamps on its own
// (createdAtUtc, updatedAtUtc, source, fullyQualifiedName, _etag).
public sealed record CreateEntityRequest(
    Guid Id,
    RegistryEntityType EntityType,
    string Name,
    string Environment,
    RegistryEntityStatus Status,
    string? Description = null,
    IReadOnlyList<RegistryTag>? Tags = null,
    string? Owner = null,
    string? AzureResourceId = null,
    string? NamespaceName = null,
    JsonElement? Metadata = null,
    Guid? ParentId = null);

// FR-020 / research §8. `_overwriteAcknowledged` is the request-body extension
// the client sends when explicitly choosing to overwrite a detected conflict.
// Endpoint handler reads it via RegistryDtoMapping.ExtractOverwriteAcknowledged
// from the raw JsonElement, but the typed shape is here for OpenAPI generation.
public sealed record UpdateEntityRequest(
    Guid Id,
    RegistryEntityType EntityType,
    string Name,
    string Environment,
    RegistryEntityStatus Status,
    string? Description = null,
    IReadOnlyList<RegistryTag>? Tags = null,
    string? Owner = null,
    string? AzureResourceId = null,
    string? NamespaceName = null,
    JsonElement? Metadata = null,
    Guid? ParentId = null,
    [property: JsonPropertyName("_overwriteAcknowledged")] bool? OverwriteAcknowledged = null);

// FR-013a status transition body. Active ↔ Deprecated; no other moves allowed.
public sealed record StatusChangeRequest(RegistryEntityStatus Status);

// List response wrapper (matches contracts/registry-api.yaml /registry GET).
public sealed record RegistryListResponse(
    IReadOnlyList<RegistryEntity> Items,
    string? ContinuationToken);

public sealed record AuditListResponse(IReadOnlyList<AuditEvent> Items);

public sealed record EnvironmentsListResponse(IReadOnlyList<string> Items);
