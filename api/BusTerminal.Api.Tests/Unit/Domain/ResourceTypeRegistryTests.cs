using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T083 / Q4. Asserts all 14 known types resolve through the registry
// the DI composition root populates, that round-trip discriminator → type → discriminator
// is stable, and that unknown discriminators are reported as not-known.
public sealed class ResourceTypeRegistryTests
{
    private static ResourceTypeRegistry BuildRegistryFromProduction()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cosmos:Endpoint"] = "https://localhost:8081",
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<BusTerminal.Api.Infrastructure.Credentials.IAzureCredentialFactory>(
            new BusTerminal.Api.Infrastructure.Credentials.AzureCredentialFactory(
                new ResourceTypeRegistryTestsHostEnv()));
        services.AddLogging();
        services.AddCosmosCanonicalStore(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ResourceTypeRegistry>();
    }

    [Fact]
    public void All_14_known_types_resolve()
    {
        var registry = BuildRegistryFromProduction();

        var expected = new (string Discriminator, Type Clr)[]
        {
            (ResourceTypeDiscriminators.Namespace, typeof(Namespace)),
            (ResourceTypeDiscriminators.Broker, typeof(Broker)),
            (ResourceTypeDiscriminators.Queue, typeof(Queue)),
            (ResourceTypeDiscriminators.Topic, typeof(Topic)),
            (ResourceTypeDiscriminators.Subscription, typeof(Subscription)),
            (ResourceTypeDiscriminators.MessageContract, typeof(MessageContract)),
            (ResourceTypeDiscriminators.ProducerApplication, typeof(ProducerApplication)),
            (ResourceTypeDiscriminators.ConsumerApplication, typeof(ConsumerApplication)),
            (ResourceTypeDiscriminators.Team, typeof(Team)),
            (ResourceTypeDiscriminators.Environment, typeof(EnvironmentResource)),
            (ResourceTypeDiscriminators.Tag, typeof(TagResource)),
            (ResourceTypeDiscriminators.Policy, typeof(Policy)),
            (ResourceTypeDiscriminators.IntegrationFlow, typeof(IntegrationFlow)),
            (ResourceTypeDiscriminators.DocumentationAsset, typeof(DocumentationAsset)),
        };

        foreach (var (discriminator, clrType) in expected)
        {
            registry.IsKnown(discriminator).Should().BeTrue($"discriminator '{discriminator}' should be known");
            registry.TryGetType(discriminator, out var resolved).Should().BeTrue();
            resolved.Should().Be(clrType);
            registry.GetDiscriminator(clrType).Should().Be(discriminator);
        }

        registry.KnownDiscriminators.Should().HaveCount(14);
    }

    [Fact]
    public void Unknown_discriminator_is_not_known()
    {
        var registry = BuildRegistryFromProduction();

        registry.IsKnown("syntheticFutureType").Should().BeFalse();
        registry.TryGetType("syntheticFutureType", out _).Should().BeFalse();
    }

    [Fact]
    public void Registering_non_resource_type_throws()
    {
        var registry = new ResourceTypeRegistry();

        var act = () => registry.Register("not-a-resource", typeof(string));
        act.Should().Throw<ArgumentException>();
    }

    private sealed class ResourceTypeRegistryTestsHostEnv : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
