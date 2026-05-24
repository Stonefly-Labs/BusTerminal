using BusTerminal.Api.Domain.Resources;

namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-008 + Edge Case "Dangling references after soft-delete" / T106.
// For every reference field on a resource, assert the referent exists. Missing
// → Error; soft-deleted → Warning (the document still exists for history).
//
// Scope: every typed FK on a known Resource subtype, plus the universal
// Tags[].TagId list on Resource. Ownership.OwningTeamId is intentionally NOT
// re-checked here — OwnershipPresenceRule (T094) already validates it with
// stricter semantics (soft-deleted Team = Error, not Warning) and double-firing
// would create operator noise.
//
// Contract on ValidationContext.RelationshipResolver: returns the referent even
// when soft-deleted so the rule can distinguish "missing" from "soft-deleted."
// This is the same contract OwnershipPresenceRule relies on.
public sealed class DanglingReferenceRule : IValidationRule
{
    public const string RuleId = "reference.dangling";

    private readonly TimeProvider _time;

    public DanglingReferenceRule(TimeProvider time)
    {
        _time = time;
    }

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        var now = _time.GetUtcNow();
        var findings = new List<ValidationFinding>();

        foreach (var tag in resource.Tags)
        {
            CheckReference(findings, context, tag.TagId, "/tags", now, "Tag");
        }

        switch (resource)
        {
            case Namespace ns when ns.ParentNamespaceId is { } parentId:
                CheckReference(findings, context, parentId, "/parentNamespaceId", now, "Namespace");
                break;

            case Subscription sub:
                CheckReference(findings, context, sub.ParentTopicId, "/parentTopicId", now, "Topic");
                foreach (var consumer in sub.Consumers)
                {
                    CheckReference(findings, context, consumer.ApplicationId, "/consumers", now, "ConsumerApplication");
                }

                break;

            case IntegrationFlow flow:
                CheckReference(findings, context, flow.ProducerApplicationId, "/producerApplicationId", now, "ProducerApplication");
                CheckReference(findings, context, flow.MessagingResourceId, "/messagingResourceId", now, "messaging resource");
                foreach (var consumerId in flow.ConsumerApplicationIds)
                {
                    CheckReference(findings, context, consumerId, "/consumerApplicationIds", now, "ConsumerApplication");
                }

                break;

            case MessageContract contract:
                foreach (var producer in contract.Producers)
                {
                    CheckReference(findings, context, producer.ApplicationId, "/producers", now, "ProducerApplication");
                }

                foreach (var consumer in contract.Consumers)
                {
                    CheckReference(findings, context, consumer.ApplicationId, "/consumers", now, "ConsumerApplication");
                }

                break;

            case Queue queue:
                foreach (var contractRef in queue.ContractAssociations)
                {
                    CheckReference(findings, context, contractRef.ContractId, "/contractAssociations", now, "MessageContract");
                }

                foreach (var producer in queue.Producers)
                {
                    CheckReference(findings, context, producer.ApplicationId, "/producers", now, "ProducerApplication");
                }

                foreach (var consumer in queue.Consumers)
                {
                    CheckReference(findings, context, consumer.ApplicationId, "/consumers", now, "ConsumerApplication");
                }

                if (queue.Deprecation?.ReplacedByResourceId is { } replacementId)
                {
                    CheckReference(findings, context, replacementId, "/deprecation/replacedByResourceId", now, "Queue");
                }

                break;

            case Topic topic:
                foreach (var subId in topic.SubscriptionIds)
                {
                    CheckReference(findings, context, subId, "/subscriptionIds", now, "Subscription");
                }

                foreach (var contractRef in topic.ContractAssociations)
                {
                    CheckReference(findings, context, contractRef.ContractId, "/contractAssociations", now, "MessageContract");
                }

                foreach (var producer in topic.Producers)
                {
                    CheckReference(findings, context, producer.ApplicationId, "/producers", now, "ProducerApplication");
                }

                break;

            case DocumentationAsset doc:
                foreach (var attachedId in doc.AttachedResourceIds)
                {
                    CheckReference(findings, context, attachedId, "/attachedResourceIds", now, "attached resource");
                }

                break;

            default:
                break;
        }

        return findings;
    }

    private static void CheckReference(
        List<ValidationFinding> findings,
        ValidationContext context,
        ResourceId referentId,
        string fieldRef,
        DateTimeOffset now,
        string expectedKindForMessage)
    {
        var referent = context.RelationshipResolver(referentId);

        if (referent is null)
        {
            findings.Add(new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Reference at {fieldRef} → {referentId} does not resolve to any {expectedKindForMessage} resource.",
                EvaluatedAt: now,
                FieldRef: fieldRef,
                RelationshipRef: referentId));
            return;
        }

        if (referent.IsDeleted)
        {
            findings.Add(new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Warning,
                Message: $"Reference at {fieldRef} → {referentId} resolves to a soft-deleted {expectedKindForMessage} resource.",
                EvaluatedAt: now,
                FieldRef: fieldRef,
                RelationshipRef: referentId));
        }
    }
}
