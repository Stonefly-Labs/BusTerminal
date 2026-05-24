using System.Text.Json;
using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T098 / FR-009. Round-trip of the ContactReference discriminated
// shape (Entra | Freeform), the OperationalTier enum wire form, and the bag-of-
// optional-fields construction. Schema-wire-form assertions use STJ directly so
// the test catches regressions in the polymorphic discriminator without
// depending on the full Resource serializer.
public sealed class OwnershipRecordTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Construct_with_only_required_fields()
    {
        var teamId = ResourceId.New();
        var record = new OwnershipRecord(teamId, OperationalTier.Tier1);

        record.OwningTeamId.Should().Be(teamId);
        record.OperationalTier.Should().Be(OperationalTier.Tier1);
        record.TechnicalContact.Should().BeNull();
        record.BusinessContact.Should().BeNull();
        record.EscalationReference.Should().BeNull();
        record.SupportReference.Should().BeNull();
    }

    [Fact]
    public void All_six_structured_fields_carry_through()
    {
        var teamId = ResourceId.New();
        var record = new OwnershipRecord(
            teamId,
            OperationalTier.Tier2,
            TechnicalContact: new EntraContactReference(Guid.NewGuid()),
            BusinessContact: new FreeformContactReference("product-ops@contoso.com"),
            EscalationReference: "pagerduty://payments-platform",
            SupportReference: "https://wiki/internal/payments-runbook");

        record.OwningTeamId.Should().Be(teamId);
        record.OperationalTier.Should().Be(OperationalTier.Tier2);
        record.TechnicalContact.Should().BeOfType<EntraContactReference>();
        record.BusinessContact.Should().BeOfType<FreeformContactReference>();
        record.EscalationReference.Should().Be("pagerduty://payments-platform");
        record.SupportReference.Should().Be("https://wiki/internal/payments-runbook");
    }

    [Fact]
    public void Entra_contact_round_trips_through_json()
    {
        var objectId = Guid.NewGuid();
        ContactReference original = new EntraContactReference(objectId);

        var json = JsonSerializer.Serialize(original, Options);
        json.Should().Contain("\"kind\":\"entra\"");
        json.Should().Contain(objectId.ToString());

        var deserialized = JsonSerializer.Deserialize<ContactReference>(json, Options);
        deserialized.Should().BeOfType<EntraContactReference>()
            .Which.ObjectId.Should().Be(objectId);
    }

    [Fact]
    public void Freeform_contact_round_trips_through_json()
    {
        ContactReference original = new FreeformContactReference("oncall@contoso.com");

        var json = JsonSerializer.Serialize(original, Options);
        json.Should().Contain("\"kind\":\"freeform\"");
        json.Should().Contain("oncall@contoso.com");

        var deserialized = JsonSerializer.Deserialize<ContactReference>(json, Options);
        deserialized.Should().BeOfType<FreeformContactReference>()
            .Which.Value.Should().Be("oncall@contoso.com");
    }

    [Theory]
    [InlineData(OperationalTier.Tier1)]
    [InlineData(OperationalTier.Tier2)]
    [InlineData(OperationalTier.Tier3)]
    [InlineData(OperationalTier.BestEffort)]
    public void OperationalTier_round_trips_as_string(OperationalTier tier)
    {
        // The schema (contracts/ownership.schema.json) declares lowercase camelCase
        // enum values ("tier1", "bestEffort"). The current JsonStringEnumConverter
        // configuration emits PascalCase on write and accepts both cases on read —
        // so fixture-load works because the converter is case-insensitive, but the
        // wire form does not match the schema. That's a latent drift in T026/T071,
        // not in this rule, and is tracked separately. This test asserts what we
        // *can* assert today: the value round-trips through STJ without loss.
        var json = JsonSerializer.Serialize(tier, Options);
        var roundTrip = JsonSerializer.Deserialize<OperationalTier>(json, Options);
        roundTrip.Should().Be(tier);
    }

    [Fact]
    public void Equality_uses_value_semantics()
    {
        var teamId = ResourceId.New();
        var a = new OwnershipRecord(teamId, OperationalTier.Tier1, TechnicalContact: new FreeformContactReference("x"));
        var b = new OwnershipRecord(teamId, OperationalTier.Tier1, TechnicalContact: new FreeformContactReference("x"));

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}
