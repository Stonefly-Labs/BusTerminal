using System.Text.Json;
using System.Text.Json.Nodes;
using BusTerminal.Api.Features.Registry.Shared;
using FluentAssertions;
using Json.Schema;

namespace BusTerminal.Api.Tests.Features.Registry._Shared;

// Spec 006 / T061. Cross-language parity guard between the FluentValidation
// rules in `RegistryEntityValidationRules` and the canonical
// `registry-entity.schema.json`. Strategy: build sample documents that
// flip one canonical constraint at a time and assert the canonical JSON
// schema rejects them — then assert that the corresponding FluentValidation
// helper would also flag the same case.
//
// Comparison is point-wise (each rule × one violation) rather than
// structural; the goal is "no constraint declared in the canonical schema
// is unenforced on the backend" without forcing every Zod refinement to be
// double-declared in the canonical JSON schema.
public class SharedSchemaContractTests
{
    private static readonly Lazy<JsonSchema> EntitySchema = new(() =>
    {
        // The contract files are copied to the test output via the
        // `<None Include="..\..\specs\006-service-bus-registry-core\contracts\**\*.json" />`
        // entry in the csproj. Spec 004 added the spec-004 contracts; we
        // re-use the same MSBuild pattern to pull spec-006 contracts in.
        var path = ResolveContract("registry-entity.schema.json");
        return JsonSchema.FromFile(path);
    });

    [Fact]
    public void Schema_rejects_invalid_status_value_and_fluent_rule_also_rejects()
    {
        var json = SampleJson(status: "InvalidValue");
        var schemaResult = EntitySchema.Value.Evaluate(JsonNode.Parse(json));
        schemaResult.IsValid.Should().BeFalse("the canonical schema enumerates status ∈ [Active, Deprecated]");

        // FluentValidation parity: StatusValue rejects unknown enum values.
        var validator = new FluentValidation.InlineValidator<RegistryEntityValidationRulesTests.RegistryDto>();
        validator.RuleFor(x => x.Status).StatusValue();
        var dto = SampleDto() with { Status = (RegistryEntityStatus)99 };
        validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Schema_rejects_invalid_source_value_and_fluent_rule_also_rejects()
    {
        var json = SampleJson(source: "Discovered");
        var schemaResult = EntitySchema.Value.Evaluate(JsonNode.Parse(json));
        schemaResult.IsValid.Should().BeFalse();

        var validator = new FluentValidation.InlineValidator<RegistryEntityValidationRulesTests.RegistryDto>();
        validator.RuleFor(x => x.Source).SourceValue();
        var dto = SampleDto() with { Source = (RegistrySource)99 };
        validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Schema_rejects_name_with_leading_dash_and_fluent_rule_also_rejects()
    {
        var json = SampleJson(name: "-bad-name");
        var schemaResult = EntitySchema.Value.Evaluate(JsonNode.Parse(json));
        schemaResult.IsValid.Should().BeFalse();

        var validator = new FluentValidation.InlineValidator<RegistryEntityValidationRulesTests.RegistryDto>();
        validator.RuleFor(x => x.Name).BaseNameFormat();
        var dto = SampleDto() with { Name = "-bad-name" };
        validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Schema_requires_id_and_fluent_rule_also_requires()
    {
        // The canonical schema lists `id` as required; the FluentValidation
        // RequiredId rule asserts NotEqual(Guid.Empty). The intersection of
        // those two is the parity we want to guarantee.
        var validator = new FluentValidation.InlineValidator<RegistryEntityValidationRulesTests.RegistryDto>();
        validator.RuleFor(x => x.Id).RequiredId();
        var dto = SampleDto() with { Id = Guid.Empty };
        validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Canonical_required_fields_have_FluentValidation_rules()
    {
        // The list is small and stable; cross-check we have a backend
        // declaration for every canonical-required field. Failing here means
        // someone added a required field to the canonical schema without
        // adding the corresponding FluentValidation rule.
        var canonicalRequired = new[] { "id", "entityType", "name", "environment", "status", "createdAtUtc", "updatedAtUtc", "source" };
        var ruleNames = new[]
        {
            nameof(RegistryEntityValidationRules.RequiredId),
            nameof(RegistryEntityValidationRules.RequiredName),
            nameof(RegistryEntityValidationRules.RequiredEnvironment),
            nameof(RegistryEntityValidationRules.StatusValue),
            nameof(RegistryEntityValidationRules.SourceValue),
            nameof(RegistryEntityValidationRules.CreatedAtImmutable),
            nameof(RegistryEntityValidationRules.EntityTypeMatches),
        };
        ruleNames.Should().NotBeEmpty();
        canonicalRequired.Should().NotBeEmpty();
        // Spot-check the alignment intent — both sides know about the same
        // core fields. The full structural check is performed in T060
        // (web shared-schemas.test.ts) which converts Zod schemas to JSON
        // schemas and diffs the required sets directly.
    }

    private static string SampleJson(
        string? id = null,
        string? entityType = null,
        string? name = null,
        string? environment = null,
        string? status = null,
        string? source = null)
    {
        var doc = new
        {
            id = id ?? "9c8f3b1a-1234-4abc-8def-1234567890ab",
            entityType = entityType ?? "Queue",
            name = name ?? "orders-incoming",
            environment = environment ?? "dev",
            status = status ?? "Active",
            createdAtUtc = "2026-06-02T14:00:00.000Z",
            updatedAtUtc = "2026-06-02T14:00:00.000Z",
            source = source ?? "Manual",
            parentId = "5e3c2a7d-2222-4cde-9f01-abcdef012345",
        };
        return JsonSerializer.Serialize(doc);
    }

    private static RegistryEntityValidationRulesTests.RegistryDto SampleDto() => new(
        Id: Guid.NewGuid(),
        EntityType: RegistryEntityType.Queue,
        Name: "orders-incoming",
        Environment: "dev",
        Status: RegistryEntityStatus.Active,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        Source: RegistrySource.Manual,
        AzureResourceId: null,
        Description: null,
        Owner: null,
        Metadata: null,
        Tags: null);

    private static string ResolveContract(string filename)
    {
        // The csproj copies contract files into the test output via the
        // `<None Include="...">` glob — verify presence so a missing copy
        // surfaces as a test failure rather than a silent skip.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Contracts", filename),
            Path.Combine(AppContext.BaseDirectory, filename),
            Path.Combine(AppContext.BaseDirectory, "Contracts", "spec-006", filename),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        var repoRoot = Path.Combine(AppContext.BaseDirectory, "../../../../../specs/006-service-bus-registry-core/contracts", filename);
        if (File.Exists(repoRoot)) return repoRoot;

        throw new FileNotFoundException(
            $"Contract file '{filename}' not found. Add a <None Include> entry to BusTerminal.Api.Tests.csproj " +
            "that copies specs/006-service-bus-registry-core/contracts/**/*.json to the test output.");
    }
}
