using BusTerminal.Api.Features.Registry.Namespaces;
using BusTerminal.Api.Features.Registry.Queues;
using BusTerminal.Api.Features.Registry.Rules;
using BusTerminal.Api.Features.Registry.Subscriptions;
using BusTerminal.Api.Features.Registry.Topics;
using FluentValidation;
using FluentValidation.Results;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T074 + T075. Dispatches a RegistryEntity to the matching
// per-type validator based on its `EntityType` discriminator. Endpoint
// handlers (T075, T078) call `ValidateAsync(entity)` instead of binding to a
// concrete validator type, which keeps the polymorphic create/update path
// simple — the discriminator picks the rule set.
public sealed class RegistryValidatorDispatcher
{
    private readonly NamespaceValidator _namespaceValidator;
    private readonly QueueValidator _queueValidator;
    private readonly TopicValidator _topicValidator;
    private readonly SubscriptionValidator _subscriptionValidator;
    private readonly RuleValidator _ruleValidator;

    public RegistryValidatorDispatcher(
        NamespaceValidator namespaceValidator,
        QueueValidator queueValidator,
        TopicValidator topicValidator,
        SubscriptionValidator subscriptionValidator,
        RuleValidator ruleValidator)
    {
        _namespaceValidator = namespaceValidator;
        _queueValidator = queueValidator;
        _topicValidator = topicValidator;
        _subscriptionValidator = subscriptionValidator;
        _ruleValidator = ruleValidator;
    }

    public Task<ValidationResult> ValidateAsync(
        RegistryEntity entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        IValidator<RegistryEntity> validator = entity.EntityType switch
        {
            RegistryEntityType.Namespace => _namespaceValidator,
            RegistryEntityType.Queue => _queueValidator,
            RegistryEntityType.Topic => _topicValidator,
            RegistryEntityType.Subscription => _subscriptionValidator,
            RegistryEntityType.Rule => _ruleValidator,
            _ => throw new ArgumentOutOfRangeException(nameof(entity), entity.EntityType, "Unknown RegistryEntityType."),
        };

        return validator.ValidateAsync(entity, cancellationToken);
    }
}
