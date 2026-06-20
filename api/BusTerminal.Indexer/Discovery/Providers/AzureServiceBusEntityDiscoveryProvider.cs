using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using BusTerminal.Indexer.Discovery.Telemetry;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Indexer.Discovery.Providers;

// Spec 009 / T049 + R-01 + R-04. ARM-backed entity discovery.
// Uses Azure.ResourceManager.ServiceBus (Reader RBAC at namespace scope) to
// stream queues, topics, subscriptions, and rules. The ARM SDK's built-in
// retry policy (configured at the ArmClient level) handles 429/503/transient
// errors; this class focuses on shape translation.
//
// Composite key construction matches API-side
// `PublishedEntityIdComputer.ComposeCompositeKey` (queue: `q:{ns}/{name}`,
// topic: `t:{ns}/{name}`, subscription: `s:{ns}/{topic}/{name}`,
// rule: `r:{ns}/{topic}/{subscription}/{name}`).
public sealed partial class AzureServiceBusEntityDiscoveryProvider : IEntityDiscoveryProvider
{
    private readonly ArmClient _arm;
    private readonly ILogger<AzureServiceBusEntityDiscoveryProvider> _logger;

    public AzureServiceBusEntityDiscoveryProvider(
        ArmClient arm,
        ILogger<AzureServiceBusEntityDiscoveryProvider> logger)
    {
        _arm = arm;
        _logger = logger;
    }

    public async IAsyncEnumerable<DiscoveredEntity> StreamQueuesAsync(
        EntityDiscoveryProviderContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = DiscoveryActivitySource.Instance.StartActivity(
            DiscoveryActivitySource.SpanNames.FetchQueues, ActivityKind.Client);
        span?.SetTag(DiscoveryActivitySource.AttributeKeys.NamespaceId, context.NamespaceId);

        var namespaceResource = GetNamespaceResource(context);
        var queues = namespaceResource.GetServiceBusQueues();
        var count = 0;
        await foreach (var queue in queues.GetAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            count++;
            yield return MapQueue(context, queue.Data);
        }
        span?.SetTag(DiscoveryActivitySource.AttributeKeys.FetchCount, count);
        LogFetched("queues", count, context.NamespaceId);
    }

    public async IAsyncEnumerable<DiscoveredEntity> StreamTopicsAsync(
        EntityDiscoveryProviderContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = DiscoveryActivitySource.Instance.StartActivity(
            DiscoveryActivitySource.SpanNames.FetchTopics, ActivityKind.Client);
        span?.SetTag(DiscoveryActivitySource.AttributeKeys.NamespaceId, context.NamespaceId);

        var namespaceResource = GetNamespaceResource(context);
        var topics = namespaceResource.GetServiceBusTopics();
        var count = 0;
        await foreach (var topic in topics.GetAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            count++;
            yield return MapTopic(context, topic.Data);
        }
        span?.SetTag(DiscoveryActivitySource.AttributeKeys.FetchCount, count);
        LogFetched("topics", count, context.NamespaceId);
    }

    public async IAsyncEnumerable<DiscoveredEntity> StreamSubscriptionsAndRulesAsync(
        EntityDiscoveryProviderContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var namespaceResource = GetNamespaceResource(context);
        var topics = namespaceResource.GetServiceBusTopics();
        var subscriptionCount = 0;
        var ruleCount = 0;
        await foreach (var topic in topics.GetAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            using var subsSpan = DiscoveryActivitySource.Instance.StartActivity(
                DiscoveryActivitySource.SpanNames.FetchSubscriptions, ActivityKind.Client);
            subsSpan?.SetTag(DiscoveryActivitySource.AttributeKeys.NamespaceId, context.NamespaceId);

            var topicName = topic.Data.Name;
            var subscriptions = topic.GetServiceBusSubscriptions();
            await foreach (var sub in subscriptions.GetAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                subscriptionCount++;
                yield return MapSubscription(context, topicName, sub.Data);

                using var rulesSpan = DiscoveryActivitySource.Instance.StartActivity(
                    DiscoveryActivitySource.SpanNames.FetchRules, ActivityKind.Client);
                rulesSpan?.SetTag(DiscoveryActivitySource.AttributeKeys.NamespaceId, context.NamespaceId);
                var rules = sub.GetServiceBusRules();
                await foreach (var rule in rules.GetAllAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    ruleCount++;
                    yield return MapRule(context, topicName, sub.Data.Name, rule.Data);
                }
            }
        }
        LogFetched("subscriptions", subscriptionCount, context.NamespaceId);
        LogFetched("rules", ruleCount, context.NamespaceId);
    }

    private ServiceBusNamespaceResource GetNamespaceResource(EntityDiscoveryProviderContext context)
    {
        var id = ServiceBusNamespaceResource.CreateResourceIdentifier(
            context.AzureSubscriptionId,
            context.ResourceGroup,
            context.NamespaceName);
        return _arm.GetServiceBusNamespaceResource(id);
    }

    // ── Mappers ──────────────────────────────────────────────────────────

    private static DiscoveredEntity MapQueue(EntityDiscoveryProviderContext context, ServiceBusQueueData data)
    {
        var azureSourced = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$type"] = "Queue",
            ["azureResourceId"] = data.Id?.ToString(),
            ["status"] = data.Status?.ToString() ?? "Unknown",
            ["lockDuration"] = data.LockDuration?.ToString() ?? "PT1M",
            ["maxDeliveryCount"] = data.MaxDeliveryCount ?? 10,
            ["duplicateDetection"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = data.RequiresDuplicateDetection ?? false,
                ["historyTimeWindow"] = data.DuplicateDetectionHistoryTimeWindow?.ToString(),
            },
            ["deadLettering"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["deadLetterOnMessageExpiration"] = data.DeadLetteringOnMessageExpiration ?? false,
            },
            ["partitioning"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = data.EnablePartitioning ?? false,
            },
            ["session"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = data.RequiresSession ?? false,
            },
            ["forwarding"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["forwardTo"] = data.ForwardTo,
                ["forwardDeadLetteredMessagesTo"] = data.ForwardDeadLetteredMessagesTo,
            },
            ["defaultTimeToLive"] = data.DefaultMessageTimeToLive?.ToString(),
            ["maxSizeInMegabytes"] = data.MaxSizeInMegabytes,
        };
        return new DiscoveredEntity(
            EntityType: DiscoveredEntityType.Queue,
            NamespaceId: context.NamespaceId,
            Name: data.Name,
            CompositeKey: $"q:{context.NamespaceId}/{data.Name}",
            ParentCompositeKey: null,
            AzureSourced: azureSourced);
    }

    private static DiscoveredEntity MapTopic(EntityDiscoveryProviderContext context, ServiceBusTopicData data)
    {
        var azureSourced = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$type"] = "Topic",
            ["azureResourceId"] = data.Id?.ToString(),
            ["status"] = data.Status?.ToString() ?? "Unknown",
            ["duplicateDetection"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = data.RequiresDuplicateDetection ?? false,
                ["historyTimeWindow"] = data.DuplicateDetectionHistoryTimeWindow?.ToString(),
            },
            ["partitioning"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = data.EnablePartitioning ?? false,
            },
            ["defaultTimeToLive"] = data.DefaultMessageTimeToLive?.ToString(),
            ["maxSizeInMegabytes"] = data.MaxSizeInMegabytes,
        };
        return new DiscoveredEntity(
            EntityType: DiscoveredEntityType.Topic,
            NamespaceId: context.NamespaceId,
            Name: data.Name,
            CompositeKey: $"t:{context.NamespaceId}/{data.Name}",
            ParentCompositeKey: null,
            AzureSourced: azureSourced);
    }

    private static DiscoveredEntity MapSubscription(EntityDiscoveryProviderContext context, string topicName, ServiceBusSubscriptionData data)
    {
        var azureSourced = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$type"] = "Subscription",
            ["azureResourceId"] = data.Id?.ToString(),
            ["status"] = data.Status?.ToString() ?? "Unknown",
            ["lockDuration"] = data.LockDuration?.ToString() ?? "PT1M",
            ["maxDeliveryCount"] = data.MaxDeliveryCount ?? 10,
            ["deadLettering"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["deadLetterOnMessageExpiration"] = data.DeadLetteringOnMessageExpiration ?? false,
            },
            ["session"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = data.RequiresSession ?? false,
            },
            ["forwarding"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["forwardTo"] = data.ForwardTo,
                ["forwardDeadLetteredMessagesTo"] = data.ForwardDeadLetteredMessagesTo,
            },
            ["defaultTimeToLive"] = data.DefaultMessageTimeToLive?.ToString(),
        };
        return new DiscoveredEntity(
            EntityType: DiscoveredEntityType.Subscription,
            NamespaceId: context.NamespaceId,
            Name: data.Name,
            CompositeKey: $"s:{context.NamespaceId}/{topicName}/{data.Name}",
            ParentCompositeKey: $"t:{context.NamespaceId}/{topicName}",
            AzureSourced: azureSourced);
    }

    private static DiscoveredEntity MapRule(EntityDiscoveryProviderContext context, string topicName, string subscriptionName, ServiceBusRuleData data)
    {
        // Edge case: data-model.md §1.1 rule shape — filter/action may be null.
        string filterType = "Unknown";
        string? filterExpression = null;
        if (data.FilterType is not null)
        {
            filterType = data.FilterType.ToString() ?? "Unknown";
        }
        if (data.SqlFilter?.SqlExpression is { } sqlExpr)
        {
            filterType = "Sql";
            filterExpression = sqlExpr;
        }
        else if (data.CorrelationFilter is not null)
        {
            filterType = "Correlation";
            filterExpression = data.CorrelationFilter.CorrelationId
                ?? data.CorrelationFilter.MessageId
                ?? "(correlation filter)";
        }

        var azureSourced = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$type"] = "Rule",
            ["azureResourceId"] = data.Id?.ToString(),
            ["status"] = "Active",
            ["filterType"] = filterType,
            ["filterExpression"] = filterExpression,
            ["actionExpression"] = data.Action?.SqlExpression,
        };
        return new DiscoveredEntity(
            EntityType: DiscoveredEntityType.Rule,
            NamespaceId: context.NamespaceId,
            Name: data.Name,
            CompositeKey: $"r:{context.NamespaceId}/{topicName}/{subscriptionName}/{data.Name}",
            ParentCompositeKey: $"s:{context.NamespaceId}/{topicName}/{subscriptionName}",
            AzureSourced: azureSourced);
    }

    [LoggerMessage(EventId = 9701, Level = LogLevel.Information,
        Message = "Fetched {Scope} count={Count} namespace={NamespaceId}.")]
    private partial void LogFetched(string scope, int count, string namespaceId);
}
