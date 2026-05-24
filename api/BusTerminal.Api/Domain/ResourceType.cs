namespace BusTerminal.Api.Domain;

// Spec 004 / FR-002 / Q4. Closed-enum + opaque-string-discriminator pair.
// The persistence layer stores the string form (so unknown future types survive
// round-trip as UnknownResource); the enum gives type-safe code paths once a
// document is materialized as a known type.
public enum ResourceType
{
    Namespace,
    Broker,
    Queue,
    Topic,
    Subscription,
    MessageContract,
    ProducerApplication,
    ConsumerApplication,
    Team,
    Environment,
    Tag,
    Policy,
    IntegrationFlow,
    DocumentationAsset,
}

// String discriminators matching the JSON Schema `const` values used in
// contracts/resources/*.schema.json. Lower-camelCase by convention.
public static class ResourceTypeDiscriminators
{
    public const string Namespace = "namespace";
    public const string Broker = "broker";
    public const string Queue = "queue";
    public const string Topic = "topic";
    public const string Subscription = "subscription";
    public const string MessageContract = "messageContract";
    public const string ProducerApplication = "producerApplication";
    public const string ConsumerApplication = "consumerApplication";
    public const string Team = "team";
    public const string Environment = "environment";
    public const string Tag = "tag";
    public const string Policy = "policy";
    public const string IntegrationFlow = "integrationFlow";
    public const string DocumentationAsset = "documentationAsset";

    // Peer document — relationships live in the same Cosmos container under their
    // own discriminator; not a Resource subtype.
    public const string Relationship = "relationship";

    public static string Of(ResourceType type) => type switch
    {
        ResourceType.Namespace => Namespace,
        ResourceType.Broker => Broker,
        ResourceType.Queue => Queue,
        ResourceType.Topic => Topic,
        ResourceType.Subscription => Subscription,
        ResourceType.MessageContract => MessageContract,
        ResourceType.ProducerApplication => ProducerApplication,
        ResourceType.ConsumerApplication => ConsumerApplication,
        ResourceType.Team => Team,
        ResourceType.Environment => Environment,
        ResourceType.Tag => Tag,
        ResourceType.Policy => Policy,
        ResourceType.IntegrationFlow => IntegrationFlow,
        ResourceType.DocumentationAsset => DocumentationAsset,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown ResourceType enum value."),
    };

    public static bool TryParse(string discriminator, out ResourceType type)
    {
        switch (discriminator)
        {
            case Namespace: type = ResourceType.Namespace; return true;
            case Broker: type = ResourceType.Broker; return true;
            case Queue: type = ResourceType.Queue; return true;
            case Topic: type = ResourceType.Topic; return true;
            case Subscription: type = ResourceType.Subscription; return true;
            case MessageContract: type = ResourceType.MessageContract; return true;
            case ProducerApplication: type = ResourceType.ProducerApplication; return true;
            case ConsumerApplication: type = ResourceType.ConsumerApplication; return true;
            case Team: type = ResourceType.Team; return true;
            case Environment: type = ResourceType.Environment; return true;
            case Tag: type = ResourceType.Tag; return true;
            case Policy: type = ResourceType.Policy; return true;
            case IntegrationFlow: type = ResourceType.IntegrationFlow; return true;
            case DocumentationAsset: type = ResourceType.DocumentationAsset; return true;
            default: type = default; return false;
        }
    }
}
