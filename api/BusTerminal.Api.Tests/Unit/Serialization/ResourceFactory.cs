using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;

namespace BusTerminal.Api.Tests.Unit.Serialization;

// Test-only — minimal sample of each first-class resource type.
// Used by JsonRoundTripTests, PolymorphismTests, ExtensionPreservationTests, etc.
internal static class ResourceFactory
{
    private static readonly DateTimeOffset SampleTime = DateTimeOffset.Parse("2026-05-23T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly ResourceId SampleTeamId = ResourceId.Parse("11111111-1111-1111-1111-000000000001");

    public static AuditRecord SampleAudit() => new(
        CreatedBy: new SystemPrincipalReference("test"),
        CreatedAt: SampleTime,
        ModifiedBy: new SystemPrincipalReference("test"),
        ModifiedAt: SampleTime);

    public static OwnershipRecord SampleOwnership() => new(
        OwningTeamId: SampleTeamId,
        OperationalTier: OperationalTier.Tier1);

    public static SemanticVersion SampleVersion() => new(1, 0, 0);

    public static IReadOnlyList<Resource> OneOfEachType() =>
    [
        BuildNamespace(),
        BuildBroker(),
        BuildQueue(),
        BuildTopic(),
        BuildSubscription(),
        BuildMessageContract(),
        BuildProducerApplication(),
        BuildConsumerApplication(),
        BuildTeam(),
        BuildEnvironmentResource(),
        BuildTagResource(),
        BuildPolicy(),
        BuildIntegrationFlow(),
        BuildDocumentationAsset(),
    ];

    public static Namespace BuildNamespace() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Namespace,
        Name = new ResourceName("payments"),
        DisplayName = "Payments",
        NamespacePath = new NamespacePath("enterprise/payments"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
    };

    public static Broker BuildBroker() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Broker,
        Name = new ResourceName("primary-bus"),
        DisplayName = "Primary Bus",
        NamespacePath = new NamespacePath("enterprise/payments"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        BrokerKind = "AzureServiceBus",
        Endpoint = "sb://primary.servicebus.windows.net/",
        Capabilities = new BrokerCapabilities(true, true, true, true),
    };

    public static Queue BuildQueue() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Queue,
        Name = new ResourceName("orders-q"),
        DisplayName = "Orders Queue",
        NamespacePath = new NamespacePath("enterprise/payments/q"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        QueueKind = "AzureServiceBus",
        Ordering = OrderingPolicy.Fifo,
        Partitioned = false,
    };

    public static Topic BuildTopic() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Topic,
        Name = new ResourceName("orders-t"),
        DisplayName = "Orders Topic",
        NamespacePath = new NamespacePath("enterprise/payments/t"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        Ordering = OrderingPolicy.Unordered,
    };

    public static Subscription BuildSubscription() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Subscription,
        Name = new ResourceName("risk-sub"),
        DisplayName = "Risk Subscription",
        NamespacePath = new NamespacePath("enterprise/payments/sub"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        ParentTopicId = ResourceId.New(),
        DeliverySemantics = DeliverySemantics.AtLeastOnce,
    };

    public static MessageContract BuildMessageContract() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.MessageContract,
        Name = new ResourceName("order-placed"),
        DisplayName = "OrderPlaced v1",
        NamespacePath = new NamespacePath("enterprise/payments/contracts"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        Format = ContractFormat.JsonSchema,
        SchemaReference = SchemaReference.FromExternalUri("https://schemas.example.com/order-placed/v1.json"),
        Compatibility = CompatibilityIndicator.Backward,
    };

    public static ProducerApplication BuildProducerApplication() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.ProducerApplication,
        Name = new ResourceName("orders-api"),
        DisplayName = "Orders API",
        NamespacePath = new NamespacePath("enterprise/payments/apps"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        ApplicationKind = "WebService",
    };

    public static ConsumerApplication BuildConsumerApplication() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.ConsumerApplication,
        Name = new ResourceName("risk-engine"),
        DisplayName = "Risk Engine",
        NamespacePath = new NamespacePath("enterprise/payments/apps"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        ApplicationKind = "BatchJob",
    };

    public static Team BuildTeam() => new()
    {
        Id = SampleTeamId,
        ResourceType = ResourceTypeDiscriminators.Team,
        Name = new ResourceName("payments-platform"),
        DisplayName = "Payments Platform",
        NamespacePath = new NamespacePath("enterprise/payments"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Slug = "payments-platform",
        OperationalTier = OperationalTier.Tier1,
    };

    public static EnvironmentResource BuildEnvironmentResource() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Environment,
        Name = new ResourceName("production"),
        DisplayName = "Production",
        NamespacePath = new NamespacePath("enterprise/envs"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Classification = EnvironmentClassification.Production,
        Region = "eastus2",
    };

    public static TagResource BuildTagResource() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Tag,
        Name = new ResourceName("payments"),
        DisplayName = "Payments",
        NamespacePath = new NamespacePath("enterprise/tags"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Category = "domain",
    };

    public static Policy BuildPolicy() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Policy,
        Name = new ResourceName("retention"),
        DisplayName = "Retention policy",
        NamespacePath = new NamespacePath("enterprise/payments/policies"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        PolicyKind = "Retention",
        RuleBody = "{}",
        Scope = new PolicyScope(PolicyScopeKind.Namespace, TargetNamespacePath: "enterprise/payments"),
    };

    public static IntegrationFlow BuildIntegrationFlow() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.IntegrationFlow,
        Name = new ResourceName("orders-to-risk"),
        DisplayName = "Orders → Risk",
        NamespacePath = new NamespacePath("enterprise/payments/flows"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        Ownership = SampleOwnership(),
        ProducerApplicationId = ResourceId.New(),
        MessagingResourceId = ResourceId.New(),
        ConsumerApplicationIds = [ResourceId.New()],
    };

    public static DocumentationAsset BuildDocumentationAsset() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.DocumentationAsset,
        Name = new ResourceName("runbook"),
        DisplayName = "Runbook",
        NamespacePath = new NamespacePath("enterprise/docs"),
        Lifecycle = LifecycleState.Active,
        Version = SampleVersion(),
        Audit = SampleAudit(),
        AssetKind = DocumentationAssetKind.Runbook,
        Uri = "https://wiki.example.com/runbook",
    };

    public static Extensions BuildExtensions(IDictionary<string, JsonElement> entries) =>
        new(entries.ToDictionary(kv => kv.Key, kv => kv.Value));
}
