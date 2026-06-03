using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Infrastructure.Search;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T032. One-stop registration for the registry slice. Consumed by
// `Program.cs` via `builder.Services.AddRegistryFeature(builder.Configuration)`.
//
// The order mirrors the dependency graph: options first, then persistence,
// then validators (autoscan of the assembly), then the helpers
// (ConcurrencyConflictMapper, ChildCountChecker, ISearchClient).
public static class RegistryServiceCollectionExtensions
{
    public static IServiceCollection AddRegistryFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Options binding + validation
        services.AddOptions<CosmosRegistryOptions>()
            .Bind(configuration.GetSection(CosmosRegistryOptions.SectionName));
        services.AddSingleton<IValidateOptions<CosmosRegistryOptions>, CosmosRegistryOptionsValidator>();

        services.AddOptions<AiSearchOptions>()
            .Bind(configuration.GetSection(AiSearchOptions.SectionName));
        services.AddSingleton<IValidateOptions<AiSearchOptions>, AiSearchOptionsValidator>();

        // Persistence stores. CosmosClient is registered by
        // AddCosmosCanonicalStore (spec 004) — we reuse it here so the registry
        // slice does not stand up a second client.
        services.AddScoped<IRegistryEntityStore, CosmosRegistryEntityStore>();
        services.AddScoped<IAuditEventStore, CosmosAuditEventStore>();

        // Per-entity-type FluentValidators (T074) live in
        // Features/Registry/{Namespaces,Queues,Topics,Subscriptions,Rules}.
        // Auto-scan picks them up.
        services.AddValidatorsFromAssemblyContaining<RegistryDtoMapping>();

        // Helpers used by the endpoint layer. ConcurrencyConflictMapper and
        // ChildCountChecker are stateless and register as singletons. The
        // DTO mapper (T041) and endpoints builder (T040) are added in
        // their own files in Phase 3.
        services.AddSingleton<ConcurrencyConflictMapper>();
        services.AddSingleton<ChildCountChecker>();
        services.AddSingleton<RegistryDtoMapping>();

        // ISearchClient resolves to AzureAiSearchClient (T036). Registered as
        // a singleton because SearchClient is thread-safe and pooled.
        services.AddSingleton<ISearchClient, AzureAiSearchClient>();

        // IMemoryCache is required by EnvironmentsEndpoint (T103c) — register
        // here so its consumers don't need to add it separately.
        services.AddMemoryCache();

        // System TimeProvider for testable time-of-day. Spec-004 already
        // registers it via TryAddSingleton; we mirror the call to keep the
        // registry slice's wiring self-contained.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
