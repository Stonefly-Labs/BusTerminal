namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-007. Matches contracts/resources/message-contract.schema.json.
// Per-version lifecycle / compatibility validation lands in US4
// (ContractCompatibilityRule, T115); this record carries the shape only.
public sealed record MessageContract : Resource
{
    public required ContractFormat Format { get; init; }

    public required SchemaReference SchemaReference { get; init; }

    public IReadOnlyCollection<ExamplePayload> ExamplePayloads { get; init; } = [];

    public required CompatibilityIndicator Compatibility { get; init; }

    public IReadOnlyCollection<ApplicationReference> Producers { get; init; } = [];

    public IReadOnlyCollection<ApplicationReference> Consumers { get; init; } = [];

    public DeprecationStatus? DeprecationStatus { get; init; }

    public ContractValidationMetadata? ValidationMetadata { get; init; }
}

public enum ContractFormat
{
    JsonSchema,
    Avro,
    Protobuf,
    XmlSchema,
    CloudEvents,
    Custom,
}

// `inline` and `externalUri` are mutually exclusive (schema oneOf) — enforced by the
// constructor below rather than the type system to keep the wire form a flat object.
public sealed record SchemaReference
{
    public string? Inline { get; init; }
    public string? ExternalUri { get; init; }

    public static SchemaReference FromInline(string inline) =>
        new() { Inline = inline };

    public static SchemaReference FromExternalUri(string uri) =>
        new() { ExternalUri = uri };
}

public sealed record ExamplePayload(string Name, System.Text.Json.JsonElement Value, string? ContentType = null);

public sealed record DeprecationStatus(
    bool IsDeprecated,
    DateOnly? ScheduledRetirementDate = null,
    SemanticVersionRef? ReplacementVersion = null);

public sealed record ContractValidationMetadata(
    DateTimeOffset? LastExternalValidationAt = null,
    string? LastExternalValidatorId = null);
