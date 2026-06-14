using System.Text.Json;
using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / data-model.md §1 + §2. Canonical registry entity. The base
// record carries every shared field; concrete records pin the `entityType`
// discriminator at construction time. ParentId stays `Guid?` on the base
// because the persistence layer reads heterogeneous documents via the
// discriminator — the per-type validators (T074) enforce the
// required/null shape at the API boundary.
//
// The wire-shape contract lives in
// `specs/006-service-bus-registry-core/contracts/registry-entity.schema.json`
// and is verified by SharedSchemaContractTests (T061).
public record RegistryEntity(
    Guid Id,
    RegistryEntityType EntityType,
    string Name,
    string Environment,
    RegistryEntityStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    RegistrySource Source,
    string? FullyQualifiedName = null,
    string? Description = null,
    IReadOnlyList<RegistryTag>? Tags = null,
    string? Owner = null,
    string? AzureResourceId = null,
    string? NamespaceName = null,
    JsonElement? Metadata = null,
    Guid? ParentId = null,
    string? Etag = null) : IRegistryEntity
{
    // The persisted JSON shape uses `[]` (not null) for empty tag arrays.
    // Default to an empty list so concrete records and DTO mappers can rely
    // on a non-null collection at every read site.
    public IReadOnlyList<RegistryTag> Tags { get; init; } = Tags ?? Array.Empty<RegistryTag>();
}

// Spec 006 / data-model.md §1.1. Root of the messaging hierarchy. `ParentId`
// is null by construction; `NamespaceName` echoes `Name` per the contract.
//
// Spec 008 / data-model.md §1.1 adds 15 nullable fields below the
// constructor to support Onboarded-source documents (validation-verified,
// Entra-backed ownership, lifecycle state, business metadata). All fields
// stay null on legacy Manual-source records — back-compat is preserved by
// construction. Validators enforce presence per source: Manual leaves them
// null, Onboarded requires them.
public sealed record RegistryNamespace : RegistryEntity
{
    public RegistryNamespace(
        Guid id,
        string name,
        string environment,
        RegistryEntityStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        RegistrySource source,
        string? fullyQualifiedName = null,
        string? description = null,
        IReadOnlyList<RegistryTag>? tags = null,
        string? owner = null,
        string? azureResourceId = null,
        JsonElement? metadata = null,
        string? etag = null)
        : base(
            id,
            RegistryEntityType.Namespace,
            name,
            environment,
            status,
            createdAtUtc,
            updatedAtUtc,
            source,
            fullyQualifiedName,
            description,
            tags,
            owner,
            azureResourceId,
            NamespaceName: name,
            metadata,
            ParentId: null,
            etag)
    {
    }

    // Spec 008 / data-model.md §1.1 — Onboarded-source-only fields.
    public string? DisplayName { get; init; }
    public Guid? SubscriptionId { get; init; }
    public string? SubscriptionName { get; init; }
    public string? ResourceGroup { get; init; }
    public Guid? TenantId { get; init; }
    public string? Region { get; init; }
    public string? BusinessUnit { get; init; }
    public string? ProductOrApplication { get; init; }
    public string? CostCenter { get; init; }
    public string? Notes { get; init; }
    public LifecycleStatus? LifecycleStatus { get; init; }
    public ValidationStatus? ValidationStatus { get; init; }
    public Guid? LastValidationRunId { get; init; }
    public DateTimeOffset? LastValidatedAtUtc { get; init; }
    public OwnershipBlock? Ownership { get; init; }
    public OnboardingActor? OnboardingActor { get; init; }
}

// Spec 006 / data-model.md §1.2. Queues live under a namespace.
public sealed record RegistryQueue : RegistryEntity
{
    public RegistryQueue(
        Guid id,
        string name,
        string environment,
        RegistryEntityStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        RegistrySource source,
        Guid parentId,
        string? fullyQualifiedName = null,
        string? description = null,
        IReadOnlyList<RegistryTag>? tags = null,
        string? owner = null,
        string? azureResourceId = null,
        string? namespaceName = null,
        JsonElement? metadata = null,
        string? etag = null)
        : base(
            id,
            RegistryEntityType.Queue,
            name,
            environment,
            status,
            createdAtUtc,
            updatedAtUtc,
            source,
            fullyQualifiedName,
            description,
            tags,
            owner,
            azureResourceId,
            namespaceName,
            metadata,
            parentId,
            etag)
    {
    }
}

// Spec 006 / data-model.md §1.3. Topics live under a namespace.
public sealed record RegistryTopic : RegistryEntity
{
    public RegistryTopic(
        Guid id,
        string name,
        string environment,
        RegistryEntityStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        RegistrySource source,
        Guid parentId,
        string? fullyQualifiedName = null,
        string? description = null,
        IReadOnlyList<RegistryTag>? tags = null,
        string? owner = null,
        string? azureResourceId = null,
        string? namespaceName = null,
        JsonElement? metadata = null,
        string? etag = null)
        : base(
            id,
            RegistryEntityType.Topic,
            name,
            environment,
            status,
            createdAtUtc,
            updatedAtUtc,
            source,
            fullyQualifiedName,
            description,
            tags,
            owner,
            azureResourceId,
            namespaceName,
            metadata,
            parentId,
            etag)
    {
    }
}

// Spec 006 / data-model.md §1.4. Subscriptions live under a topic.
public sealed record RegistrySubscription : RegistryEntity
{
    public RegistrySubscription(
        Guid id,
        string name,
        string environment,
        RegistryEntityStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        RegistrySource source,
        Guid parentId,
        string? fullyQualifiedName = null,
        string? description = null,
        IReadOnlyList<RegistryTag>? tags = null,
        string? owner = null,
        string? azureResourceId = null,
        string? namespaceName = null,
        JsonElement? metadata = null,
        string? etag = null)
        : base(
            id,
            RegistryEntityType.Subscription,
            name,
            environment,
            status,
            createdAtUtc,
            updatedAtUtc,
            source,
            fullyQualifiedName,
            description,
            tags,
            owner,
            azureResourceId,
            namespaceName,
            metadata,
            parentId,
            etag)
    {
    }
}

// Spec 006 / data-model.md §1.5. First-class Rule entity (diverges from
// spec-004 where Rule is embedded in Subscription — recorded in
// data-model.md §7 Vocabulary Alignment).
public sealed record RegistryRule : RegistryEntity
{
    public RegistryRule(
        Guid id,
        string name,
        string environment,
        RegistryEntityStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        RegistrySource source,
        Guid parentId,
        string? fullyQualifiedName = null,
        string? description = null,
        IReadOnlyList<RegistryTag>? tags = null,
        string? owner = null,
        string? azureResourceId = null,
        string? namespaceName = null,
        JsonElement? metadata = null,
        string? etag = null)
        : base(
            id,
            RegistryEntityType.Rule,
            name,
            environment,
            status,
            createdAtUtc,
            updatedAtUtc,
            source,
            fullyQualifiedName,
            description,
            tags,
            owner,
            azureResourceId,
            namespaceName,
            metadata,
            parentId,
            etag)
    {
    }
}
