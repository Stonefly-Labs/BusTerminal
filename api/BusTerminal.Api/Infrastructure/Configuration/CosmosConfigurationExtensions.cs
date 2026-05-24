using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
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

        // ResourceTypeRegistry is pre-populated with the 14 first-class types at
        // composition time (Spec 004 / T072). Hand-maintained static map per
        // tech-stack.md §1 — explicit over reflection. New types added in future
        // slices register themselves here.
        services.AddSingleton(_ =>
        {
            var registry = new ResourceTypeRegistry();
            registry.Register(ResourceTypeDiscriminators.Namespace, typeof(Namespace));
            registry.Register(ResourceTypeDiscriminators.Broker, typeof(Broker));
            registry.Register(ResourceTypeDiscriminators.Queue, typeof(Queue));
            registry.Register(ResourceTypeDiscriminators.Topic, typeof(Topic));
            registry.Register(ResourceTypeDiscriminators.Subscription, typeof(Subscription));
            registry.Register(ResourceTypeDiscriminators.MessageContract, typeof(MessageContract));
            registry.Register(ResourceTypeDiscriminators.ProducerApplication, typeof(ProducerApplication));
            registry.Register(ResourceTypeDiscriminators.ConsumerApplication, typeof(ConsumerApplication));
            registry.Register(ResourceTypeDiscriminators.Team, typeof(Team));
            registry.Register(ResourceTypeDiscriminators.Environment, typeof(EnvironmentResource));
            registry.Register(ResourceTypeDiscriminators.Tag, typeof(TagResource));
            registry.Register(ResourceTypeDiscriminators.Policy, typeof(Policy));
            registry.Register(ResourceTypeDiscriminators.IntegrationFlow, typeof(IntegrationFlow));
            registry.Register(ResourceTypeDiscriminators.DocumentationAsset, typeof(DocumentationAsset));
            return registry;
        });

        // Serialization: the JsonResourceSerializer + its converters are singletons
        // so a single STJ options graph is shared across persistence and
        // import/export paths. The unknown-resource factory routes through
        // UnknownResourceFactory.Create to materialize UnknownResource records that
        // preserve the raw payload for diagnostic surfacing (Q4).
        services.AddSingleton<ExtensionsJsonConverter>();
        services.AddSingleton<Func<string, JsonElement, JsonSerializerOptions, Resource>>(_ =>
            UnknownResourceFactory.Create);
        services.AddSingleton<ResourceJsonConverter>();
        services.AddSingleton<JsonResourceSerializer>();
        services.AddSingleton<IResourceSerializer>(sp => sp.GetRequiredService<JsonResourceSerializer>());
        services.AddSingleton<CosmosStjSerializer>();

        services.AddSingleton<CosmosClientFactory>();
        services.AddSingleton(sp => sp.GetRequiredService<CosmosClientFactory>().Create());

        services.AddScoped<ICanonicalResourceStore, CosmosCanonicalResourceStore>();
        services.AddScoped<IChangeEventLog, CosmosChangeEventLog>();

        // Validation engine is scoped — a single context per request / per fixture
        // load. Rule implementations are registered as singletons (pure-CPU, no
        // per-request state). Per-story modules layer in additional rules.
        services.AddScoped<ValidationEngine>();
        services.AddSingleton<IValidationRule, RequiredFieldsRule>();
        services.AddSingleton<IValidationRule, NamingStandardsRule>();
        services.AddSingleton<IValidationRule, UnknownResourceTypeRule>();
        services.AddSingleton<IValidationRule, OwnershipPresenceRule>();

        // Spec 004 / FR-003 — NamespaceInheritance traverses the parent chain via
        // ICanonicalResourceStore.QueryAsync, so it shares the store's scope.
        services.AddScoped<NamespaceInheritance>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
