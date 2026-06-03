using System.Text.Json;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / data-model.md §2. Canonical field set shared by every concrete
// registry entity (Namespace / Queue / Topic / Subscription / Rule). The
// interface is implemented by an abstract base record (RegistryEntity) so
// concrete records inherit the field shape for free.
//
// Naming on this interface matches `data-model.md §9 Naming Cross-Reference`
// exactly — persisted JSON (camelCase via the API's STJ options), OpenAPI DTOs,
// AI Search index fields, and OTel attributes all key off these names.
public interface IRegistryEntity
{
    Guid Id { get; }
    RegistryEntityType EntityType { get; }
    string Name { get; }
    string? FullyQualifiedName { get; }
    string? Description { get; }
    IReadOnlyList<RegistryTag> Tags { get; }
    string? Owner { get; }
    string Environment { get; }
    RegistryEntityStatus Status { get; }
    DateTimeOffset CreatedAtUtc { get; }
    DateTimeOffset UpdatedAtUtc { get; }
    RegistrySource Source { get; }
    string? AzureResourceId { get; }
    string? NamespaceName { get; }
    JsonElement? Metadata { get; }
    Guid? ParentId { get; }
    string? Etag { get; }
}
