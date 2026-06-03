using System.Text.Json;
using BusTerminal.Indexer.Indexing;
using FluentAssertions;

namespace BusTerminal.Indexer.Tests;

// Spec 006 / T050. Field-mapping coverage from contracts/indexer-events.md §3:
//   - every canonical field projection
//   - tag-key lowercase projection
//   - metadata-flat dot-path keys (nested objects)
//   - null normalizations (description / owner / azureResourceId → empty string)
public class SearchDocumentMapperTests
{
    private readonly SearchDocumentMapper _sut = new();

    [Fact]
    public void Projects_canonical_fields_identity()
    {
        var id = Guid.NewGuid().ToString("D");
        var parentId = Guid.NewGuid().ToString("D");
        var item = new RegistryEntityChangeFeedItem
        {
            Id = id,
            EntityType = "Queue",
            Name = "orders-incoming",
            FullyQualifiedName = "orders-prod/orders-incoming",
            Description = "primary intake",
            Owner = "payments",
            Environment = "dev",
            Status = "Active",
            NamespaceName = "orders-prod",
            AzureResourceId = "/subscriptions/x/.../queues/orders-incoming",
            ParentId = parentId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        var doc = _sut.ToSearchDocument(item);

        doc["id"].Should().Be(id);
        doc["entityType"].Should().Be("Queue");
        doc["name"].Should().Be("orders-incoming");
        doc["fullyQualifiedName"].Should().Be("orders-prod/orders-incoming");
        doc["description"].Should().Be("primary intake");
        doc["owner"].Should().Be("payments");
        doc["environment"].Should().Be("dev");
        doc["status"].Should().Be("Active");
        doc["namespaceName"].Should().Be("orders-prod");
        doc["parentId"].Should().Be(parentId);
        doc["brokerKind"].Should().Be("AzureServiceBus");
    }

    [Fact]
    public void Lowercases_tag_keys_and_dedups()
    {
        var item = new RegistryEntityChangeFeedItem
        {
            Id = Guid.NewGuid().ToString("D"),
            EntityType = "Queue",
            Tags =
            [
                new RegistryTagItem { Key = "Owner", Value = "Alice" },
                new RegistryTagItem { Key = "owner", Value = "Bob" },
                new RegistryTagItem { Key = "Tier", Value = "1" },
            ],
        };

        var doc = _sut.ToSearchDocument(item);
        var lowered = (string?[])doc["tagKeysLower"]!;

        lowered.Should().BeEquivalentTo(new[] { "owner", "tier" });
    }

    [Fact]
    public void Flattens_metadata_with_dot_paths()
    {
        var metadata = JsonDocument.Parse("{\"policy\":{\"retention\":{\"days\":30}}, \"flag\":true}").RootElement;
        var item = new RegistryEntityChangeFeedItem
        {
            Id = Guid.NewGuid().ToString("D"),
            EntityType = "Queue",
            Metadata = metadata,
        };

        var doc = _sut.ToSearchDocument(item);
        var flat = (string[])doc["metadataFlat"]!;

        flat.Should().Contain("policy.retention.days=30");
        flat.Should().Contain("flag=true");
    }

    [Fact]
    public void Normalizes_null_description_owner_azureResourceId_to_empty_string()
    {
        var item = new RegistryEntityChangeFeedItem
        {
            Id = Guid.NewGuid().ToString("D"),
            EntityType = "Queue",
            Description = null,
            Owner = null,
            AzureResourceId = null,
        };

        var doc = _sut.ToSearchDocument(item);

        doc["description"].Should().Be(string.Empty);
        doc["owner"].Should().Be(string.Empty);
        doc["azureResourceId"].Should().Be(string.Empty);
    }

    [Fact]
    public void Preserves_null_parentId()
    {
        var item = new RegistryEntityChangeFeedItem
        {
            Id = Guid.NewGuid().ToString("D"),
            EntityType = "Namespace",
            ParentId = null,
        };

        var doc = _sut.ToSearchDocument(item);

        doc["parentId"].Should().BeNull();
    }

    [Fact]
    public void Flattens_top_level_string_metadata()
    {
        var metadata = JsonDocument.Parse("{\"k\":\"v\"}").RootElement;
        var item = new RegistryEntityChangeFeedItem
        {
            Id = Guid.NewGuid().ToString("D"),
            EntityType = "Queue",
            Metadata = metadata,
        };

        var doc = _sut.ToSearchDocument(item);
        var flat = (string[])doc["metadataFlat"]!;

        flat.Should().ContainSingle().Which.Should().Be("k=v");
    }
}
