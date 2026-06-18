using System.Collections.Concurrent;
using System.Net;
using Azure.ResourceManager;
using BusTerminal.Api.Features.Discovery.Shared;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Identity;
using BusTerminal.Api.Infrastructure.ServiceBus;
using BusTerminal.Api.Tests.Features.Namespaces;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusTerminal.Api.Tests.Features.Discovery;

// Spec 009 / Phase 3 tests. Standalone WebApplicationFactory mirroring the
// spec-008 namespace test factory but swapping in spec-009 in-memory stores.
// Tests assert against the in-memory state directly so no Cosmos / Service
// Bus emulator is required.
public sealed class DiscoveryContractFactory : WebApplicationFactory<Program>
{
    public InMemoryRegistryEntityStore EntityStore { get; } = new();
    public InMemoryAuditEventStore AuditStore { get; } = new();
    public InMemoryDiscoveryLockStore LockStore { get; } = new();
    public InMemoryDiscoveryRunStore RunStore { get; } = new();
    public InMemoryPublishedEntitySearchClient PublishedEntitySearch { get; } = new();
    public InMemoryPublishedEntityStore PublishedEntities { get; } = new();
    public SpyDiscoveryRequestPublisher Publisher { get; } = new();
    public InMemoryNamespaceValidationRunStore ValidationRunStore { get; } = new();
    public StubArmProbe ArmProbe { get; } = new();
    public StubGraphPicker GraphPicker { get; } = new();
    public StubArmSubscriptionTenantResolver TenantResolver { get; } = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    public static Guid WorkloadPrincipalId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);
        builder.UseSetting("AzureAd:TenantId", "development");
        builder.UseSetting("AzureAd:Instance", "https://login.microsoftonline.com/");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("AzureAd:Audience", "api://busterminal-dev");
        builder.UseSetting("Cosmos:Endpoint", "https://example-cosmos.documents.azure.com:443/");
        builder.UseSetting("Cosmos:Database", "canonical");
        builder.UseSetting("Cosmos:Containers:Resources", "resources");
        builder.UseSetting("Cosmos:Containers:ChangeEvents", "change-events");
        builder.UseSetting("CosmosRegistry:Database", "canonical");
        builder.UseSetting("CosmosRegistry:EntitiesContainer", "registry-entities");
        builder.UseSetting("CosmosRegistry:AuditContainer", "registry-audit");
        builder.UseSetting("CosmosRegistry:LeasesContainer", "registry-entities-leases");
        builder.UseSetting("CosmosRegistry:ValidationRunsContainer", "namespace-validation-runs");
        builder.UseSetting("AiSearch:Endpoint", "https://example-search.search.windows.net");
        builder.UseSetting("AiSearch:IndexName", "registry-entities-v1");
        builder.UseSetting(WorkloadIdentityProvider.ConfigurationKey, WorkloadPrincipalId.ToString("D"));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRegistryEntityStore>();
            services.AddSingleton<IRegistryEntityStore>(EntityStore);
            services.RemoveAll<IAuditEventStore>();
            services.AddSingleton<IAuditEventStore>(AuditStore);

            services.RemoveAll<CosmosClient>();
            services.AddSingleton(_ => CreateNullCosmosClient());
            services.RemoveAll<ISearchClient>();
            services.AddSingleton<ISearchClient>(new FakeSearchClient());

            services.RemoveAll<INamespaceValidationRunStore>();
            services.AddSingleton<INamespaceValidationRunStore>(ValidationRunStore);

            services.RemoveAll<IArmNamespaceProbe>();
            services.AddSingleton<IArmNamespaceProbe>(ArmProbe);

            services.RemoveAll<IGraphPrincipalPicker>();
            services.AddSingleton<IGraphPrincipalPicker>(GraphPicker);

            services.RemoveAll<IArmSubscriptionTenantResolver>();
            services.AddSingleton<IArmSubscriptionTenantResolver>(TenantResolver);

            services.RemoveAll<ArmClient>();
            services.AddSingleton(_ => new ArmClient(new Azure.Identity.DefaultAzureCredential()));

            // Spec 009 discovery overrides.
            services.RemoveAll<IDiscoveryLockStore>();
            services.AddSingleton<IDiscoveryLockStore>(LockStore);
            services.RemoveAll<IDiscoveryRunStore>();
            services.AddSingleton<IDiscoveryRunStore>(RunStore);
            services.RemoveAll<IDiscoveryRequestPublisher>();
            services.AddSingleton<IDiscoveryRequestPublisher>(Publisher);
            services.RemoveAll<IPublishedEntityStore>();
            services.AddSingleton<IPublishedEntityStore>(PublishedEntities);

            services.RemoveAll<IPublishedEntitySearchClient>();
            services.AddSingleton<IPublishedEntitySearchClient>(PublishedEntitySearch);
        });
    }

    private static CosmosClient CreateNullCosmosClient()
    {
        var options = new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new SilentHttpMessageHandler()),
            ConnectionMode = ConnectionMode.Gateway,
        };
        return new CosmosClient(
            "https://example-cosmos.documents.azure.com:443/",
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            options);
    }

    private sealed class SilentHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}

public sealed class InMemoryDiscoveryLockStore : IDiscoveryLockStore
{
    private readonly ConcurrentDictionary<string, LockState> _locks = new();
    public sealed record LockState(string CurrentRunId, DateTimeOffset ExpectedReleaseByUtc);

    public Task<DiscoveryLockAcquisition> TryAcquireAsync(string namespaceId, string newRunId, string podId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_locks.TryGetValue(namespaceId, out var existing))
        {
            if (existing.ExpectedReleaseByUtc > now)
            {
                return Task.FromResult(new DiscoveryLockAcquisition(DiscoveryLockOutcome.Coalesced, existing.CurrentRunId, null));
            }
            var stolen = existing.CurrentRunId;
            _locks[namespaceId] = new LockState(newRunId, now.AddMinutes(5));
            return Task.FromResult(new DiscoveryLockAcquisition(DiscoveryLockOutcome.Stolen, newRunId, stolen));
        }
        _locks[namespaceId] = new LockState(newRunId, now.AddMinutes(5));
        return Task.FromResult(new DiscoveryLockAcquisition(DiscoveryLockOutcome.Acquired, newRunId, null));
    }

    public Task ReleaseAsync(string namespaceId, string runId, CancellationToken cancellationToken)
    {
        _locks.TryRemove(namespaceId, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryDiscoveryRunStore : IDiscoveryRunStore
{
    private readonly ConcurrentDictionary<string, DiscoveryRun> _runs = new();

    public IReadOnlyCollection<DiscoveryRun> All() => _runs.Values.ToArray();

    public Task<DiscoveryRun> CreateAsync(DiscoveryRun run, CancellationToken cancellationToken)
    {
        _runs[run.Id] = run;
        return Task.FromResult(run);
    }

    public Task<DiscoveryRun?> GetAsync(string runId, string namespaceId, CancellationToken cancellationToken)
    {
        if (_runs.TryGetValue(runId, out var run) && run.NamespaceId == namespaceId)
        {
            return Task.FromResult<DiscoveryRun?>(run);
        }
        return Task.FromResult<DiscoveryRun?>(null);
    }

    public Task<DiscoveryRunPage> ListByNamespaceAsync(string namespaceId, int pageSize, string? continuationToken, CancellationToken cancellationToken)
    {
        // Test-only offset cursor: tokens are decimal integers that point at
        // the next item to return in the reverse-chronological sequence. The
        // production CosmosDiscoveryRunStore hands back Cosmos-native opaque
        // tokens; the contract here is "opaque string carries forward state".
        var offset = 0;
        if (!string.IsNullOrEmpty(continuationToken) && int.TryParse(continuationToken, out var parsed) && parsed > 0)
        {
            offset = parsed;
        }

        var ordered = _runs.Values
            .Where(r => r.NamespaceId == namespaceId)
            .OrderByDescending(r => r.StartedUtc)
            .ToArray();

        var page = ordered
            .Skip(offset)
            .Take(pageSize)
            .ToArray();

        var nextOffset = offset + page.Length;
        string? next = nextOffset < ordered.Length ? nextOffset.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

        return Task.FromResult(new DiscoveryRunPage(page, next));
    }

    public Task<DiscoveryRun> UpdateStatusAsync(string runId, string namespaceId, DiscoveryRunStatusUpdate update, string? ifMatch, CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(runId, out var existing))
        {
            throw new InvalidOperationException($"Run {runId} not found.");
        }
        var updated = existing with
        {
            Status = update.Status ?? existing.Status,
            CompletedUtc = update.CompletedUtc ?? existing.CompletedUtc,
            DurationMs = update.DurationMs ?? existing.DurationMs,
            QueueCount = update.QueueCount ?? existing.QueueCount,
            TopicCount = update.TopicCount ?? existing.TopicCount,
            SubscriptionCount = update.SubscriptionCount ?? existing.SubscriptionCount,
            RuleCount = update.RuleCount ?? existing.RuleCount,
            NewCount = update.NewCount ?? existing.NewCount,
            UpdatedCount = update.UpdatedCount ?? existing.UpdatedCount,
            UnchangedCount = update.UnchangedCount ?? existing.UnchangedCount,
            MissingCount = update.MissingCount ?? existing.MissingCount,
            Failure = update.Failure ?? existing.Failure,
        };
        _runs[runId] = updated;
        return Task.FromResult(updated);
    }

    public Task AppendCoalescedRequestAsync(string runId, string namespaceId, CoalescedRequest request, CancellationToken cancellationToken)
    {
        if (_runs.TryGetValue(runId, out var existing))
        {
            var coalesced = existing.CoalescedRequests.Append(request).ToArray();
            _runs[runId] = existing with { CoalescedRequests = coalesced };
        }
        return Task.CompletedTask;
    }
}

public sealed class SpyDiscoveryRequestPublisher : IDiscoveryRequestPublisher
{
    public List<DiscoveryRequestEnvelope> Published { get; } = new();

    public Task PublishAsync(DiscoveryRequestEnvelope envelope, CancellationToken cancellationToken)
    {
        Published.Add(envelope);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPublishedEntityStore : IPublishedEntityStore
{
    private readonly ConcurrentDictionary<string, PublishedEntityDetail> _byId = new();

    public IReadOnlyCollection<PublishedEntityDetail> All() => _byId.Values.ToArray();

    public void Seed(PublishedEntityDetail detail) => _byId[detail.Entity.Id] = detail;

    public Task UpsertAzureSourcedAsync(DiscoveredEntityUpsert upsert, string? ifMatch, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<PublishedEntityProjection?> GetForDiscoveryAsync(string entityId, string environment, CancellationToken cancellationToken)
        => Task.FromResult<PublishedEntityProjection?>(null);

    public async IAsyncEnumerable<PublishedEntityProjection> ListMissingCandidatesAsync(
        string namespaceId,
        string environment,
        DateTimeOffset olderThan,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield break;
    }

    public Task<PublishedEntityDetail?> GetDetailAsync(string entityId, string environment, CancellationToken cancellationToken)
    {
        if (_byId.TryGetValue(entityId, out var detail) && detail.Entity.Environment == environment)
        {
            return Task.FromResult<PublishedEntityDetail?>(detail);
        }
        return Task.FromResult<PublishedEntityDetail?>(null);
    }
}

public sealed class InMemoryPublishedEntitySearchClient : IPublishedEntitySearchClient
{
    public PublishedEntitySearchResults NextResults { get; set; } = new(Array.Empty<PublishedEntitySearchHit>(), 0);
    public PublishedEntitySearchRequest? LastRequest { get; private set; }
    public bool ThrowOnSearch { get; set; }
    public int ThrowStatus { get; set; } = 503;

    public Task<PublishedEntitySearchResults> SearchAsync(PublishedEntitySearchRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (ThrowOnSearch)
        {
            throw new Azure.RequestFailedException(ThrowStatus, "AI Search is unavailable", "ServiceUnavailable", innerException: null);
        }
        return Task.FromResult(NextResults);
    }

    public void Reset()
    {
        NextResults = new PublishedEntitySearchResults(Array.Empty<PublishedEntitySearchHit>(), 0);
        LastRequest = null;
        ThrowOnSearch = false;
    }
}
