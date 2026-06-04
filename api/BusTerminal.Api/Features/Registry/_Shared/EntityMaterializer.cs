using System.Text.Json;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006. Materializes a CreateEntityRequest / UpdateEntityRequest into the
// matching concrete RegistryEntity subtype (RegistryNamespace, RegistryQueue,
// RegistryTopic, RegistrySubscription, RegistryRule). Single source of truth
// for the entity-construction pattern so endpoint handlers don't duplicate it.
internal static class EntityMaterializer
{
    public static RegistryEntity FromCreateRequest(
        CreateEntityRequest request,
        DateTimeOffset stampedAtUtc,
        string? fullyQualifiedName = null,
        string? namespaceName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tags = request.Tags ?? Array.Empty<RegistryTag>();
        // Clone the JsonElement so the persisted entity carries an
        // independently-owned tree — the source JsonDocument from model
        // binding goes out of scope at the end of the request and any
        // subsequent reads would throw on a disposed element.
        var metadata = request.Metadata?.Clone();
        return Build(
            id: request.Id,
            entityType: request.EntityType,
            name: request.Name,
            environment: request.Environment,
            status: request.Status,
            createdAtUtc: stampedAtUtc,
            updatedAtUtc: stampedAtUtc,
            source: RegistrySource.Manual,
            tags: tags,
            description: request.Description,
            owner: request.Owner,
            azureResourceId: request.AzureResourceId,
            namespaceName: namespaceName ?? request.NamespaceName,
            metadata: metadata,
            parentId: request.ParentId,
            fullyQualifiedName: fullyQualifiedName,
            etag: null);
    }

    public static RegistryEntity FromUpdateRequest(
        UpdateEntityRequest request,
        RegistryEntity persisted,
        DateTimeOffset stampedAtUtc,
        string? fullyQualifiedName = null,
        string? namespaceName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(persisted);
        var tags = request.Tags ?? Array.Empty<RegistryTag>();
        var metadata = request.Metadata?.Clone();
        return Build(
            id: request.Id,
            entityType: request.EntityType,
            name: request.Name,
            environment: request.Environment,
            status: request.Status,
            createdAtUtc: persisted.CreatedAtUtc,
            updatedAtUtc: stampedAtUtc,
            source: persisted.Source,
            tags: tags,
            description: request.Description,
            owner: request.Owner,
            azureResourceId: request.AzureResourceId,
            namespaceName: namespaceName ?? request.NamespaceName ?? persisted.NamespaceName,
            metadata: metadata,
            parentId: request.ParentId,
            fullyQualifiedName: fullyQualifiedName,
            etag: persisted.Etag);
    }

    private static RegistryEntity Build(
        Guid id,
        RegistryEntityType entityType,
        string name,
        string environment,
        RegistryEntityStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        RegistrySource source,
        IReadOnlyList<RegistryTag> tags,
        string? description,
        string? owner,
        string? azureResourceId,
        string? namespaceName,
        JsonElement? metadata,
        Guid? parentId,
        string? fullyQualifiedName,
        string? etag)
    {
        return entityType switch
        {
            RegistryEntityType.Namespace => new RegistryNamespace(
                id, name, environment, status, createdAtUtc, updatedAtUtc, source,
                fullyQualifiedName, description, tags, owner, azureResourceId, metadata, etag),
            RegistryEntityType.Queue => new RegistryQueue(
                id, name, environment, status, createdAtUtc, updatedAtUtc, source,
                parentId ?? throw new InvalidOperationException("Queue requires parentId."),
                fullyQualifiedName, description, tags, owner, azureResourceId, namespaceName, metadata, etag),
            RegistryEntityType.Topic => new RegistryTopic(
                id, name, environment, status, createdAtUtc, updatedAtUtc, source,
                parentId ?? throw new InvalidOperationException("Topic requires parentId."),
                fullyQualifiedName, description, tags, owner, azureResourceId, namespaceName, metadata, etag),
            RegistryEntityType.Subscription => new RegistrySubscription(
                id, name, environment, status, createdAtUtc, updatedAtUtc, source,
                parentId ?? throw new InvalidOperationException("Subscription requires parentId."),
                fullyQualifiedName, description, tags, owner, azureResourceId, namespaceName, metadata, etag),
            RegistryEntityType.Rule => new RegistryRule(
                id, name, environment, status, createdAtUtc, updatedAtUtc, source,
                parentId ?? throw new InvalidOperationException("Rule requires parentId."),
                fullyQualifiedName, description, tags, owner, azureResourceId, namespaceName, metadata, etag),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown RegistryEntityType."),
        };
    }
}
