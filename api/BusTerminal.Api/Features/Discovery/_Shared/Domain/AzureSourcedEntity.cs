using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / data-model.md §1.1. Discriminated union of the four entity-type
// projections. Discriminator key `$type` maps to the EntityType name so the
// shape is stable across Cosmos persistence, AI Search projection, and the
// public OpenAPI surface.
//
// System.Text.Json polymorphic serialization is configured here via the
// attributes; runtime registration lives in AzureSourcedJsonConverter.cs.

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AzureSourcedQueue), typeDiscriminator: nameof(EntityType.Queue))]
[JsonDerivedType(typeof(AzureSourcedTopic), typeDiscriminator: nameof(EntityType.Topic))]
[JsonDerivedType(typeof(AzureSourcedSubscription), typeDiscriminator: nameof(EntityType.Subscription))]
[JsonDerivedType(typeof(AzureSourcedRule), typeDiscriminator: nameof(EntityType.Rule))]
public abstract record AzureSourcedEntity(
    string AzureResourceId,
    string? ArmEtag,
    string Status);

// Shared sub-shapes referenced from multiple entity-type records.
public sealed record AzureSourcedDuplicateDetection(bool Enabled, string? HistoryTimeWindow);

public sealed record AzureSourcedDeadLettering(bool DeadLetterOnMessageExpiration);

public sealed record AzureSourcedPartitioning(bool Enabled);

public sealed record AzureSourcedSession(bool Enabled);

public sealed record AzureSourcedForwarding(string? ForwardTo, string? ForwardDeadLetteredMessagesTo);

// data-model.md §1.1 — Queue projection.
public sealed record AzureSourcedQueue(
    string AzureResourceId,
    string? ArmEtag,
    string Status,
    string LockDuration,
    int MaxDeliveryCount,
    AzureSourcedDuplicateDetection DuplicateDetection,
    AzureSourcedDeadLettering DeadLettering,
    AzureSourcedPartitioning Partitioning,
    AzureSourcedSession Session,
    AzureSourcedForwarding Forwarding,
    string? DefaultTimeToLive,
    int? MaxSizeInMegabytes) : AzureSourcedEntity(AzureResourceId, ArmEtag, Status);

// data-model.md §1.1 — Topic projection.
public sealed record AzureSourcedTopic(
    string AzureResourceId,
    string? ArmEtag,
    string Status,
    AzureSourcedDuplicateDetection DuplicateDetection,
    AzureSourcedPartitioning Partitioning,
    string? DefaultTimeToLive,
    int? MaxSizeInMegabytes) : AzureSourcedEntity(AzureResourceId, ArmEtag, Status);

// data-model.md §1.1 — Subscription projection.
public sealed record AzureSourcedSubscription(
    string AzureResourceId,
    string? ArmEtag,
    string Status,
    string LockDuration,
    int MaxDeliveryCount,
    AzureSourcedDeadLettering DeadLettering,
    AzureSourcedSession Session,
    AzureSourcedForwarding Forwarding,
    string? DefaultTimeToLive) : AzureSourcedEntity(AzureResourceId, ArmEtag, Status);

// data-model.md §1.1 — Rule projection. FilterExpression and ActionExpression
// are nullable per the documented edge case where Azure returns neither.
public sealed record AzureSourcedRule(
    string AzureResourceId,
    string? ArmEtag,
    string Status,
    string FilterType,
    string? FilterExpression,
    string? ActionExpression) : AzureSourcedEntity(AzureResourceId, ArmEtag, Status);
