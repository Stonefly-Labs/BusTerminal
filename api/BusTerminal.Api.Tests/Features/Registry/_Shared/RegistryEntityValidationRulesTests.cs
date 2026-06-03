using System.Text.Json;
using BusTerminal.Api.Features.Registry.Shared;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;

namespace BusTerminal.Api.Tests.Features.Registry._Shared;

// Spec 006 / T027. Every rule in RegistryEntityValidationRules has at least
// one pass + one fail case. Per-entity-type name specialization (T074) is
// exercised by additional cases below (Story 1 AC #2 — one valid and one
// invalid name per type).
public class RegistryEntityValidationRulesTests
{
    [Fact]
    public void RequiredId_passes_for_nonempty_guid()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Id).RequiredId());
        var dto = SampleDto with { Id = Guid.NewGuid() };
        validator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RequiredId_fails_for_empty_guid()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Id).RequiredId());
        var dto = SampleDto with { Id = Guid.Empty };
        Assert.False(validator.Validate(dto).IsValid);
    }

    [Fact]
    public void RequiredEnvironment_passes_for_short_value()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Environment).RequiredEnvironment());
        validator.Validate(SampleDto with { Environment = "dev" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RequiredEnvironment_fails_for_missing(string? value)
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Environment).RequiredEnvironment());
        validator.Validate(SampleDto with { Environment = value! }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void BaseNameFormat_passes_for_basic_name()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Name).BaseNameFormat());
        validator.Validate(SampleDto with { Name = "orders-incoming" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("-leading-dash")]
    [InlineData(" leading-space")]
    [InlineData("with space")]
    public void BaseNameFormat_fails_for_invalid_chars(string bad)
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Name).BaseNameFormat());
        validator.Validate(SampleDto with { Name = bad }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("orders-prod")]    // 11 chars, valid for namespace
    [InlineData("aaaaa1")]         // 6 chars (min), valid
    public void NamespaceNameFormat_passes_for_valid_names(string name)
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Name).NamespaceNameFormat());
        validator.Validate(SampleDto with { Name = name }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ab")]                                    // too short (< 6)
    [InlineData("1-starts-with-digit")]                   // must start with letter
    [InlineData("ends-with-dash-")]                       // ends with hyphen
    [InlineData("orders.prod")]                           // dots not allowed in namespace
    [InlineData("abcdefghijklmnopqrstuvwxyz-12345678901234567890zzzzzz")] // 52 chars (> 50)
    public void NamespaceNameFormat_fails_for_invalid_names(string name)
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Name).NamespaceNameFormat());
        validator.Validate(SampleDto with { Name = name }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void StatusValue_passes_for_known_value()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Status).StatusValue());
        validator.Validate(SampleDto with { Status = RegistryEntityStatus.Active }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void StatusValue_fails_for_unknown_value()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Status).StatusValue());
        validator.Validate(SampleDto with { Status = (RegistryEntityStatus)42 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SourceValue_passes_for_manual()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Source).SourceValue());
        validator.Validate(SampleDto with { Source = RegistrySource.Manual }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SourceValue_fails_for_non_manual()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Source).SourceValue());
        validator.Validate(SampleDto with { Source = (RegistrySource)99 }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/orders-prod/queues/orders-incoming", RegistryEntityType.Queue, true)]
    [InlineData("/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/orders-prod/queues/orders-incoming", RegistryEntityType.Topic, false)]
    [InlineData("not-an-arm-id", RegistryEntityType.Queue, false)]
    [InlineData(null, RegistryEntityType.Queue, true)]
    public void AzureResourceIdFormat_validates_shape_and_type_segment(string? id, RegistryEntityType type, bool expected)
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.AzureResourceId).AzureResourceIdFormat(type));
        validator.Validate(SampleDto with { AzureResourceId = id }).IsValid.Should().Be(expected);
    }

    [Fact]
    public void TagShape_passes_for_well_formed_tags()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Tags).TagShape());
        var tags = new List<RegistryTag> { new("Owner", "PaymentsTeam") };
        validator.Validate(SampleDto with { Tags = tags }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TagShape_fails_for_empty_value()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Tags).TagShape());
        var tags = new List<RegistryTag> { new("Owner", "") };
        validator.Validate(SampleDto with { Tags = tags }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MetadataSize_passes_for_small_payload()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Metadata).MetadataSize());
        var metadata = JsonSerializer.Deserialize<JsonElement>("{\"k\":\"v\"}");
        validator.Validate(SampleDto with { Metadata = metadata }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MetadataSize_fails_for_oversized_payload()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Metadata).MetadataSize());
        var bigString = new string('x', 200_000);
        var metadata = JsonSerializer.Deserialize<JsonElement>($"{{\"big\":\"{bigString}\"}}");
        validator.Validate(SampleDto with { Metadata = metadata }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EntityTypeMatches_passes_when_equal()
    {
        var validator = BuildValidator(b =>
            b.RuleFor(x => x.EntityType).EntityTypeMatches(RegistryEntityType.Queue));
        validator.Validate(SampleDto with { EntityType = RegistryEntityType.Queue }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EntityTypeMatches_fails_when_different()
    {
        var validator = BuildValidator(b =>
            b.RuleFor(x => x.EntityType).EntityTypeMatches(RegistryEntityType.Queue));
        validator.Validate(SampleDto with { EntityType = RegistryEntityType.Topic }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void IdMatches_passes_when_equal()
    {
        var id = Guid.NewGuid();
        var validator = BuildValidator(b => b.RuleFor(x => x.Id).IdMatches(id));
        validator.Validate(SampleDto with { Id = id }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void IdMatches_fails_when_different()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Id).IdMatches(Guid.NewGuid()));
        validator.Validate(SampleDto with { Id = Guid.NewGuid() }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreatedAtImmutable_passes_when_equal()
    {
        var now = DateTimeOffset.UtcNow;
        var validator = BuildValidator(b => b.RuleFor(x => x.CreatedAtUtc).CreatedAtImmutable(now));
        validator.Validate(SampleDto with { CreatedAtUtc = now }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreatedAtImmutable_fails_when_changed()
    {
        var validator = BuildValidator(b =>
            b.RuleFor(x => x.CreatedAtUtc).CreatedAtImmutable(DateTimeOffset.UtcNow.AddDays(-1)));
        validator.Validate(SampleDto with { CreatedAtUtc = DateTimeOffset.UtcNow }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DescriptionLength_fails_when_exceeds_limit()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Description).DescriptionLength());
        validator.Validate(SampleDto with { Description = new string('x', 5_000) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void OwnerLength_passes_when_under_limit()
    {
        var validator = BuildValidator(b => b.RuleFor(x => x.Owner).OwnerLength());
        validator.Validate(SampleDto with { Owner = "payments-platform" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void NormalizeTagsForWrite_preserves_first_write_casing()
    {
        var persisted = new List<RegistryTag> { new("Owner", "PaymentsTeam") };
        var submitted = new List<RegistryTag> { new("OWNER", "AnotherTeam") };

        var normalized = RegistryEntityValidationRules.NormalizeTagsForWrite(submitted, persisted);

        normalized.Should().HaveCount(1);
        normalized[0].Key.Should().Be("Owner");
        normalized[0].Value.Should().Be("AnotherTeam");
    }

    [Fact]
    public void NormalizeTagsForWrite_preserves_multi_value_per_key()
    {
        var submitted = new List<RegistryTag>
        {
            new("Owner", "Alice"),
            new("Owner", "Bob"),
        };

        var normalized = RegistryEntityValidationRules.NormalizeTagsForWrite(submitted, persistedTags: null);

        normalized.Should().HaveCount(2);
        normalized.Select(t => t.Value).Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public void NormalizeTagsForWrite_canonicalizes_within_submission()
    {
        var submitted = new List<RegistryTag>
        {
            new("Owner", "Alice"),
            new("OWNER", "Bob"),
        };

        var normalized = RegistryEntityValidationRules.NormalizeTagsForWrite(submitted, persistedTags: null);

        normalized.Should().HaveCount(2);
        normalized.Select(t => t.Key).Should().AllBeEquivalentTo("Owner");
    }

    private static InlineValidator<RegistryDto> BuildValidator(Action<InlineValidator<RegistryDto>> configure)
    {
        var v = new InlineValidator<RegistryDto>();
        configure(v);
        return v;
    }

    private static readonly RegistryDto SampleDto = new(
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

    // Mutable-record DTO used to exercise each rule in isolation. The
    // production validators in T074 compose the same rules against the
    // canonical RegistryEntity record.
    public sealed record RegistryDto(
        Guid Id,
        RegistryEntityType EntityType,
        string Name,
        string Environment,
        RegistryEntityStatus Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        RegistrySource Source,
        string? AzureResourceId,
        string? Description,
        string? Owner,
        JsonElement? Metadata,
        IReadOnlyList<RegistryTag>? Tags);
}
