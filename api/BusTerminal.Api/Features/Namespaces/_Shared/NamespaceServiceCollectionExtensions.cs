using Azure.ResourceManager;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Namespaces.Lifecycle;
using BusTerminal.Api.Features.Namespaces.Metadata;
using BusTerminal.Api.Features.Namespaces.Onboarding;
using BusTerminal.Api.Features.Namespaces.Ownership;
using BusTerminal.Api.Infrastructure.Credentials;
using BusTerminal.Api.Infrastructure.Graph;
using BusTerminal.Api.Infrastructure.Identity;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Infrastructure.ServiceBus;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / T046. One-stop registration for the namespace-onboarding slice.
// Consumed by Program.cs via
// `builder.Services.AddNamespaceOnboardingFeature(builder.Configuration)`.
//
// Ordering mirrors the dependency graph: ARM + Graph + Identity adapters
// first, then persistence ports, then FluentValidation + validators. The
// existing CosmosClient + IAzureCredentialFactory + GraphServiceClient
// registrations are reused — this extension does NOT duplicate spec-004 /
// spec-006 wiring.
public static class NamespaceServiceCollectionExtensions
{
    public static IServiceCollection AddNamespaceOnboardingFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Spec 008 / research §5 — per-check + aggregate ARM probe timeouts.
        services
            .AddOptions<ArmNamespaceProbeOptions>()
            .Bind(configuration.GetSection(ArmNamespaceProbeOptions.SectionName));

        // ArmClient (singleton). Authenticates via the workload UAMI's
        // DefaultAzureCredential chain — same pattern as CosmosClientFactory.
        services.AddSingleton(sp =>
        {
            var credentialFactory = sp.GetRequiredService<IAzureCredentialFactory>();
            var userAssignedClientId = configuration["AZURE_CLIENT_ID"];
            var credential = credentialFactory.CreateCredential(userAssignedClientId);
            return new ArmClient(credential);
        });

        // Spec 008 / research §1, §3, §14. Named HttpClient for the ARM
        // management plane + the Service Bus management endpoint. The
        // ApiReachability check uses the same factory but distinguishes the
        // call via a per-request URI.
        services.AddHttpClient("ArmManagement");

        // Spec 008 / research §10. NamespaceArmIdParser + its resolver.
        services.AddSingleton<IArmSubscriptionTenantResolver, ArmSubscriptionTenantResolver>();
        services.AddSingleton<NamespaceArmIdParser>();

        // ARM probe + Graph picker + workload identity provider.
        services.AddSingleton<IArmNamespaceProbe, ArmNamespaceProbe>();
        services.AddSingleton<IGraphPrincipalPicker, GraphPrincipalPicker>();
        services.AddSingleton<WorkloadIdentityProvider>();

        // Persistence port for the new namespace-validation-runs container.
        services.AddScoped<INamespaceValidationRunStore, CosmosNamespaceValidationRunStore>();

        // Spec 008 / T038–T041. OnboardingValidator depends on the scoped
        // IRegistryEntityStore + INamespaceValidationRunStore — register
        // scoped to satisfy the DI scope validator. Stateless validators stay
        // singletons.
        services.AddScoped<OnboardingValidator>();
        services.AddSingleton<UpdateMetadataValidator>();
        services.AddSingleton<UpdateOwnershipValidator>();
        services.AddSingleton<LifecycleTransitionValidator>();

        // Spec 008 / T027.
        services.AddSingleton<NamespaceDtoMapping>();

        // Time abstraction for the OnboardingValidator's freshness window.
        services.TryAddSingleton(TimeProvider.System);

        // Spec 008 / research §15. The CanAdministerNamespaces policy is
        // registered by AddBusTerminalRolePolicies (already called by Program).
        // We re-affirm here with TryAddSingleton so multi-call sites stay safe.
        _ = services;

        return services;
    }
}
