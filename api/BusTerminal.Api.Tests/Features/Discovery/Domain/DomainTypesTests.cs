using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.Domain;

// Spec 009 / T014 — exercise the foundational domain pieces: enum string
// serialization, record equality, polymorphic JSON round-trips across all
// four AzureSourced subtypes, and the PublishedEntityIdComputer (T029a).
public sealed class DomainTypesTests
{
    private static JsonSerializerOptions Options => AzureSourcedJsonConfig.CreateOptions();

    [Theory]
    [InlineData(EntityType.Queue, "Queue")]
    [InlineData(EntityType.Topic, "Topic")]
    [InlineData(EntityType.Subscription, "Subscription")]
    [InlineData(EntityType.Rule, "Rule")]
    public void EntityType_Serializes_As_String(EntityType type, string expectedJsonLiteral)
    {
        var json = JsonSerializer.Serialize(type, Options);
        json.Should().Be($"\"{expectedJsonLiteral}\"");
    }

    [Theory]
    [InlineData(LifecycleStatus.Active)]
    [InlineData(LifecycleStatus.Missing)]
    [InlineData(LifecycleStatus.Archived)]
    public void LifecycleStatus_RoundTrips(LifecycleStatus status)
    {
        var json = JsonSerializer.Serialize(status, Options);
        var back = JsonSerializer.Deserialize<LifecycleStatus>(json, Options);
        back.Should().Be(status);
    }

    [Fact]
    public void EntityServiceAssociation_RecordEquality_HoldsOnAllFields()
    {
        var ts = DateTimeOffset.Parse("2026-06-17T14:35:01Z");
        var a = new EntityServiceAssociation("esa_01HZ", "svc_01HKY", EntityServiceRole.Owner, ts, "user1");
        var b = new EntityServiceAssociation("esa_01HZ", "svc_01HKY", EntityServiceRole.Owner, ts, "user1");
        var c = a with { Role = EntityServiceRole.Producer };

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void AzureSourcedQueue_PolymorphicRoundTrip_PreservesDiscriminator()
    {
        AzureSourcedEntity original = new AzureSourcedQueue(
            AzureResourceId: "/subscriptions/.../namespaces/ns/queues/orders",
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

        var json = JsonSerializer.Serialize(original, Options);
        json.Should().Contain("\"$type\":\"Queue\"");
        var back = JsonSerializer.Deserialize<AzureSourcedEntity>(json, Options);
        back.Should().BeOfType<AzureSourcedQueue>();
        back.Should().Be(original);
    }

    [Fact]
    public void AzureSourcedTopic_RoundTrip_PreservesDiscriminator()
    {
        AzureSourcedEntity original = new AzureSourcedTopic(
            "/subscriptions/.../topics/t1", null, "Active",
            new AzureSourcedDuplicateDetection(false, null),
            new AzureSourcedPartitioning(false),
            "P7D",
            5120);
        var json = JsonSerializer.Serialize(original, Options);
        json.Should().Contain("\"$type\":\"Topic\"");
        var back = JsonSerializer.Deserialize<AzureSourcedEntity>(json, Options);
        back.Should().BeOfType<AzureSourcedTopic>().And.Be(original);
    }

    [Fact]
    public void AzureSourcedSubscription_RoundTrip_PreservesDiscriminator()
    {
        AzureSourcedEntity original = new AzureSourcedSubscription(
            "/subscriptions/.../topics/t1/subs/s1", null, "Active",
            "PT1M", 10,
            new AzureSourcedDeadLettering(true),
            new AzureSourcedSession(false),
            new AzureSourcedForwarding(null, null),
            "P7D");
        var json = JsonSerializer.Serialize(original, Options);
        json.Should().Contain("\"$type\":\"Subscription\"");
        var back = JsonSerializer.Deserialize<AzureSourcedEntity>(json, Options);
        back.Should().BeOfType<AzureSourcedSubscription>().And.Be(original);
    }

    [Theory]
    [InlineData("Sql", "1=1", null)]
    [InlineData("Correlation", "x=y", "SET a=b")]
    [InlineData("True", null, null)] // edge case: rule with no filter/action
    public void AzureSourcedRule_RoundTrip_HandlesNullableFields(
        string filterType, string? filterExpression, string? actionExpression)
    {
        AzureSourcedEntity original = new AzureSourcedRule(
            "/subscriptions/.../rules/r1", null, "Active",
            filterType, filterExpression, actionExpression);
        var json = JsonSerializer.Serialize(original, Options);
        json.Should().Contain("\"$type\":\"Rule\"");
        var back = JsonSerializer.Deserialize<AzureSourcedEntity>(json, Options);
        back.Should().BeOfType<AzureSourcedRule>().And.Be(original);
    }

    [Theory]
    [InlineData(EntityType.Queue, "ns_01H", null, null, null, "orders-inbox", "q:ns_01H/orders-inbox")]
    [InlineData(EntityType.Topic, "ns_01H", null, null, null, "events", "t:ns_01H/events")]
    public void PublishedEntityIdComputer_ComposesCompositeKey_ForQueueAndTopic(
        EntityType type, string ns, string? topic, string? sub, string? rule, string? leaf, string expected)
    {
        var key = PublishedEntityIdComputer.ComposeCompositeKey(type, ns, topic, sub, rule, leaf);
        key.Should().Be(expected);
    }

    [Fact]
    public void PublishedEntityIdComputer_ComposesCompositeKey_ForSubscription()
    {
        var key = PublishedEntityIdComputer.ComposeCompositeKey(
            EntityType.Subscription, "ns_01H", topicName: "events", subscriptionName: "billing");
        key.Should().Be("s:ns_01H/events/billing");
    }

    [Fact]
    public void PublishedEntityIdComputer_ComposesCompositeKey_ForRule()
    {
        var key = PublishedEntityIdComputer.ComposeCompositeKey(
            EntityType.Rule, "ns_01H", topicName: "events", subscriptionName: "billing", ruleName: "high-priority");
        key.Should().Be("r:ns_01H/events/billing/high-priority");
    }

    [Fact]
    public void PublishedEntityIdComputer_IsDeterministic()
    {
        var k1 = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, "ns_01H", leafName: "orders");
        var k2 = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, "ns_01H", leafName: "orders");
        k1.Should().Be(k2);
    }

    [Fact]
    public void PublishedEntityIdComputer_ProducesValidIdFormat()
    {
        var id = PublishedEntityIdComputer.ComputeFor(EntityType.Rule, "ns_x", "t1", "s1", "r1");
        id.Should().StartWith("pe_");
        id.Should().HaveLength(3 + 24);
        PublishedEntityIdComputer.IsValidId(id).Should().BeTrue();
    }

    [Fact]
    public void PublishedEntityIdComputer_DifferentInputs_ProduceDifferentIds()
    {
        var a = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, "ns_a", leafName: "orders");
        var b = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, "ns_b", leafName: "orders");
        var c = PublishedEntityIdComputer.ComputeFor(EntityType.Queue, "ns_a", leafName: "shipments");
        a.Should().NotBe(b);
        a.Should().NotBe(c);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-pe-prefixed")]
    [InlineData("pe_short")]
    [InlineData("pe_LOWERCASEINVALID_______")]
    public void PublishedEntityIdComputer_IsValidId_RejectsMalformed(string? id)
    {
        PublishedEntityIdComputer.IsValidId(id).Should().BeFalse();
    }

    [Fact]
    public void DiscoveryRun_IsTerminal_Reflects_Status()
    {
        var baseRun = new DiscoveryRun(
            Id: "dr_01H",
            SchemaVersion: DiscoveryRun.CurrentSchemaVersion,
            NamespaceId: "ns",
            Status: DiscoveryRunStatus.Queued,
            Trigger: DiscoveryTrigger.Manual,
            StartedUtc: DateTimeOffset.UtcNow,
            CompletedUtc: null,
            DurationMs: null,
            RequestedBy: "u1",
            QueueCount: 0, TopicCount: 0, SubscriptionCount: 0, RuleCount: 0,
            NewCount: 0, UpdatedCount: 0, UnchangedCount: 0, MissingCount: 0,
            Failure: null,
            CoalescedRequests: Array.Empty<CoalescedRequest>(),
            CorrelationId: "00-...");

        baseRun.IsTerminal.Should().BeFalse();
        (baseRun with { Status = DiscoveryRunStatus.InProgress }).IsTerminal.Should().BeFalse();
        (baseRun with { Status = DiscoveryRunStatus.Succeeded }).IsTerminal.Should().BeTrue();
        (baseRun with { Status = DiscoveryRunStatus.Failed }).IsTerminal.Should().BeTrue();
    }
}
