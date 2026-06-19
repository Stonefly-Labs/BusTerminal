using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;

namespace BusTerminal.Api.Tests.Features.Discovery.Shared;

// Spec 009 / Phase 6 tests. Builds a fully-populated PublishedEntityDetail
// + the matching PublishedEntitySearchHit so contract tests can stage
// fixtures with one helper call instead of pages of boilerplate.
internal static class PublishedEntitySeed
{
    public const string EnvDev = "dev";
    public const string NamespaceId = "ns_test";

    public static (PublishedEntityDetail Detail, PublishedEntitySearchHit Hit) Build(
        string id = "pe_AAAAAAAAAAAAAAAAAAAAAAAA",
        string etag = "\"etag-seed-0\"",
        EntityType entityType = EntityType.Queue,
        IReadOnlyList<EntityServiceAssociation>? associations = null,
        EntityRegistryMetadata? registry = null,
        LifecycleStatus lifecycle = LifecycleStatus.Active)
    {
        var azureSourced = new AzureSourcedQueue(
            AzureResourceId: "/subscriptions/x/queues/" + id,
            ArmEtag: "W/\"abc\"",
            Status: "Active",
            LockDuration: "PT1M",
            MaxDeliveryCount: 10,
            DuplicateDetection: new AzureSourcedDuplicateDetection(true, "PT10M"),
            DeadLettering: new AzureSourcedDeadLettering(true),
            Partitioning: new AzureSourcedPartitioning(false),
            Session: new AzureSourcedSession(false),
            Forwarding: new AzureSourcedForwarding(null, null),
            DefaultTimeToLive: "P14D",
            MaxSizeInMegabytes: 5120);

        var now = DateTimeOffset.UtcNow;
        var entity = new PublishedEntity(
            Id: id,
            SchemaVersion: PublishedEntity.CurrentSchemaVersion,
            EntityType: entityType,
            Environment: EnvDev,
            NamespaceId: NamespaceId,
            Name: "orders-inbox",
            DisplayName: "orders-inbox",
            CompositeKey: $"q:{NamespaceId}/orders-inbox",
            ParentEntityId: null,
            Registry: registry ?? EntityRegistryMetadata.Empty with { Description = "Curated description" },
            LifecycleStatus: lifecycle,
            LifecycleStatusChangedUtc: now,
            FirstDiscoveredUtc: now,
            LastSeenUtc: now,
            LastDiscoveryRunId: "dr_TEST00000000000000000000001",
            AzureSourced: azureSourced,
            AzureSourcedHash: "sha256:abc",
            ServiceAssociations: associations ?? Array.Empty<EntityServiceAssociation>(),
            CreatedUtc: now,
            CreatedBy: "00000000-0000-0000-0000-000000000099",
            LastModifiedUtc: now,
            LastModifiedBy: "00000000-0000-0000-0000-000000000099",
            ETag: etag);
        var detail = new PublishedEntityDetail(entity, etag);

        var hit = new PublishedEntitySearchHit(
            Id: id,
            EntityType: entityType,
            NamespaceId: NamespaceId,
            Name: entity.Name,
            ParentEntityId: null,
            LifecycleStatus: lifecycle,
            LastSeenUtc: now,
            Environment: EnvDev,
            AssociatedServiceIds: (associations ?? Array.Empty<EntityServiceAssociation>()).Select(a => a.ServiceId).Distinct().ToArray(),
            AssociationRoles: (associations ?? Array.Empty<EntityServiceAssociation>()).Select(a => a.Role).Distinct().ToArray(),
            Tags: entity.Registry.Tags);

        return (detail, hit);
    }
}
