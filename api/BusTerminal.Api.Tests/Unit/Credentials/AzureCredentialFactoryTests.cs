using Azure.Identity;
using BusTerminal.Api.Infrastructure.Credentials;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BusTerminal.Api.Tests.Unit.Credentials;

/// <summary>
/// Verifies the single credential-acquisition path used everywhere Azure
/// services are reached (FR-018). The factory must produce a
/// <see cref="DefaultAzureCredential"/> in every environment so that local
/// developer credentials and deployed Managed Identity resolve through the
/// same chain; in non-Development environments a <c>userAssignedClientId</c>
/// must short-circuit the chain to the requested MI.
/// </summary>
public sealed class AzureCredentialFactoryTests
{
    [Fact]
    public void CreateCredential_InDevelopment_ReturnsDefaultAzureCredential()
    {
        var factory = new AzureCredentialFactory(HostEnv(Environments.Development));

        var credential = factory.CreateCredential();

        credential.Should().BeOfType<DefaultAzureCredential>();
    }

    [Fact]
    public void CreateCredential_InProduction_WithClientId_ReturnsDefaultAzureCredential()
    {
        var factory = new AzureCredentialFactory(HostEnv(Environments.Production));

        var credential = factory.CreateCredential("11111111-2222-3333-4444-555555555555");

        credential.Should().BeOfType<DefaultAzureCredential>();
    }

    [Fact]
    public void BuildOptions_InDevelopment_ReturnsNull()
    {
        var options = AzureCredentialFactory.BuildOptions(
            HostEnv(Environments.Development),
            userAssignedClientId: null);

        options.Should().BeNull(
            "Development resolves the developer's identity from the full DefaultAzureCredential chain — no ManagedIdentity short-circuit");
    }

    [Fact]
    public void BuildOptions_InDevelopment_IgnoresClientId()
    {
        // Even if a client id is passed in Development we must not force a
        // ManagedIdentity-only chain; the developer's az/VSCode credential
        // has to remain reachable.
        var options = AzureCredentialFactory.BuildOptions(
            HostEnv(Environments.Development),
            userAssignedClientId: "11111111-2222-3333-4444-555555555555");

        options.Should().BeNull();
    }

    [Fact]
    public void BuildOptions_InProduction_WithClientId_SetsManagedIdentityClientId()
    {
        const string ClientId = "11111111-2222-3333-4444-555555555555";

        var options = AzureCredentialFactory.BuildOptions(
            HostEnv(Environments.Production),
            ClientId);

        options.Should().NotBeNull();
        options!.ManagedIdentityClientId.Should().Be(ClientId);
    }

    [Fact]
    public void BuildOptions_InProduction_WithoutClientId_LeavesManagedIdentityClientIdUnset()
    {
        var options = AzureCredentialFactory.BuildOptions(
            HostEnv(Environments.Production),
            userAssignedClientId: null);

        options.Should().NotBeNull();
        options!.ManagedIdentityClientId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildOptions_InProduction_WhitespaceClientId_IsTreatedAsAbsent(string clientId)
    {
        var options = AzureCredentialFactory.BuildOptions(
            HostEnv(Environments.Production),
            clientId);

        options.Should().NotBeNull();
        options!.ManagedIdentityClientId.Should().BeNull();
    }

    private static IHostEnvironment HostEnv(string environmentName) =>
        new TestHostEnvironment(environmentName);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "BusTerminal.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
