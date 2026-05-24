using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Configuration;

// Spec 004 / FR-018 + FR-020 + FR-025 + Q5. Composes the canonical-store DI
// graph. Validation rules registered as IValidationRule (multi-registration) by
// the per-story modules — this method only registers the engine, the resource
// type registry, the persistence adapter, and the JSON serialization stack.
public static class CosmosConfigurationExtensions
{
    public static IServiceCollection AddCosmosCanonicalStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // ValidateOnStart is intentionally NOT called: the existing
        // WebApplicationFactory-based integration tests boot Program.cs without
        // a Cosmos section. The validator still runs lazily when CosmosOptions is
        // first resolved (i.e., the first persistence call) so misconfiguration
        // surfaces loudly at deploy time without breaking unrelated test factories.
        services.AddOptions<CosmosOptions>()
            .Bind(configuration.GetSection(CosmosOptions.SectionName));
        services.AddSingleton<IValidateOptions<CosmosOptions>, CosmosOptionsValidator>();

        // ResourceTypeRegistry is mutated at startup as per-type modules register
        // themselves (US1 / T072), so it must be a singleton.
        services.AddSingleton<ResourceTypeRegistry>();

        // Serialization: the JsonResourceSerializer + its converters are singletons
        // so a single STJ options graph is shared across persistence and
        // import/export paths. The unknown-resource factory defaults to throwing —
        // US1 T071 replaces it with a real factory that wraps the raw JsonElement.
        services.AddSingleton<ExtensionsJsonConverter>();
        services.AddSingleton<Func<string, System.Text.Json.JsonElement, Resource>>(_ =>
            (_, _) => throw new InvalidOperationException(
                "UnknownResource factory not registered. US1 / T071 wires the real factory; until then, persisting a document with an unknown resourceType is unsupported."));
        services.AddSingleton<ResourceJsonConverter>();
        services.AddSingleton<JsonResourceSerializer>();
        services.AddSingleton<IResourceSerializer>(sp => sp.GetRequiredService<JsonResourceSerializer>());
        services.AddSingleton<CosmosStjSerializer>();

        services.AddSingleton<CosmosClientFactory>();
        services.AddSingleton(sp => sp.GetRequiredService<CosmosClientFactory>().Create());

        services.AddScoped<ICanonicalResourceStore, CosmosCanonicalResourceStore>();
        services.AddScoped<IChangeEventLog, CosmosChangeEventLog>();

        // Validation engine is scoped — a single context per request / per fixture
        // load. Rule implementations are registered by per-story modules as
        // singletons (most are pure-CPU, no per-request state).
        services.AddScoped<ValidationEngine>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
