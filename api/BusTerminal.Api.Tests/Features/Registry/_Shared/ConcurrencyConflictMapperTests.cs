using System.Text.Json;
using BusTerminal.Api.Features.Registry.Shared;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry._Shared;

// Spec 006 / T042. Diff coverage: clean diff, tag-array diff (multi-value),
// metadata-object diff (nested), null↔value transitions.
public class ConcurrencyConflictMapperTests
{
    private readonly ConcurrencyConflictMapper _sut = new();

    [Fact]
    public void Single_scalar_diff_produces_one_changed_field()
    {
        var (current, submitted) = MakePair(
            currentDescription: "before",
            submittedDescription: "after");

        var response = _sut.BuildConflict(current, submitted, submittedEtag: "\"old\"");

        response.ChangedFields.Should().ContainSingle(f => f.Field == "/description");
        var change = response.ChangedFields.Single(f => f.Field == "/description");
        change.CurrentValue.Should().Be("before");
        change.SubmittedValue.Should().Be("after");
        response.CurrentEntity.Id.Should().Be(current.Id);
        response.Code.Should().Be("ConcurrencyConflict");
        response.Status.Should().Be(409);
    }

    [Fact]
    public void Tag_array_diff_detects_multi_value_per_key_changes()
    {
        var (current, submitted) = MakePair(
            currentTags: new[] { new RegistryTag("Owner", "Alice"), new RegistryTag("Owner", "Bob") },
            submittedTags: new[] { new RegistryTag("Owner", "Alice") });

        var response = _sut.BuildConflict(current, submitted, "\"old\"");

        response.ChangedFields.Should().ContainSingle(f => f.Field == "/tags");
    }

    [Fact]
    public void Metadata_nested_object_diff_is_detected()
    {
        var (current, submitted) = MakePair(
            currentMetadata: JsonDocument.Parse("{\"policy\":{\"retention\":{\"days\":30}}}").RootElement,
            submittedMetadata: JsonDocument.Parse("{\"policy\":{\"retention\":{\"days\":60}}}").RootElement);

        var response = _sut.BuildConflict(current, submitted, "\"old\"");

        response.ChangedFields.Should().ContainSingle(f => f.Field == "/metadata");
    }

    [Fact]
    public void Null_to_value_transition_is_a_changed_field()
    {
        var (current, submitted) = MakePair(
            currentDescription: null,
            submittedDescription: "now-with-description");

        var response = _sut.BuildConflict(current, submitted, "\"old\"");

        response.ChangedFields.Should().ContainSingle(f => f.Field == "/description");
        response.ChangedFields.Single().SubmittedValue.Should().Be("now-with-description");
    }

    [Fact]
    public void Identical_entities_produce_no_changed_fields()
    {
        var (current, submitted) = MakePair();

        var response = _sut.BuildConflict(current, submitted, "\"old\"");

        // The mapper's job isn't to gate on the diff; it just reports.
        // With identical inputs the changed-fields list is empty.
        response.ChangedFields.Should().BeEmpty();
    }

    [Fact]
    public void Server_managed_fields_are_excluded_from_diff()
    {
        var current = MakeQueue() with { Etag = "\"server-etag-1\"", UpdatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
        var submitted = current with { Etag = "\"server-etag-2\"", UpdatedAtUtc = DateTimeOffset.UtcNow };

        var response = _sut.BuildConflict(current, submitted, "\"old\"");

        response.ChangedFields.Should().NotContain(f => f.Field.EndsWith("etag", StringComparison.Ordinal));
        response.ChangedFields.Should().NotContain(f => f.Field == "/updatedAtUtc");
    }

    private static (RegistryEntity Current, RegistryEntity Submitted) MakePair(
        string? currentDescription = "stable",
        string? submittedDescription = "stable",
        IReadOnlyList<RegistryTag>? currentTags = null,
        IReadOnlyList<RegistryTag>? submittedTags = null,
        JsonElement? currentMetadata = null,
        JsonElement? submittedMetadata = null)
    {
        var baseQueue = MakeQueue();
        var current = baseQueue with
        {
            Description = currentDescription,
            Tags = currentTags ?? baseQueue.Tags,
            Metadata = currentMetadata ?? baseQueue.Metadata,
        };
        var submitted = baseQueue with
        {
            Description = submittedDescription,
            Tags = submittedTags ?? baseQueue.Tags,
            Metadata = submittedMetadata ?? baseQueue.Metadata,
        };
        return (current, submitted);
    }

    private static RegistryQueue MakeQueue() => new(
        id: Guid.NewGuid(),
        name: "orders-incoming",
        environment: "dev",
        status: RegistryEntityStatus.Active,
        createdAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5),
        updatedAtUtc: DateTimeOffset.UtcNow,
        source: RegistrySource.Manual,
        parentId: Guid.NewGuid(),
        description: "stable");
}
