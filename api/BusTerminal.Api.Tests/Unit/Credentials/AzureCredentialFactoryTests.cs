using Azure.Identity;
using BusTerminal.Api.Infrastructure.Credentials;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BusTerminal.Api.Tests.Unit.Credentials;

/// <summary>
/// Verifies the single credential-acquisition path used everywhere Azure
/// services are reached (FR-018).
///
/// Behaviour matrix:
///   * Development → DefaultAzureCredential (full chain, az/VSCode reachable).
///   * Non-Development + userAssignedClientId set → ManagedIdentityCredential
///     pinned to that client id (skips the chain probe; production is on
///     Container Apps where the workload UAMI is the only credible source).
///   * Non-Development + no userAssignedClientId → DefaultAzureCredential
///     fallback so missing-plumbing doesn't take the service down.
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
    public void CreateCredential_InDevelopment_IgnoresClientId()
    {
        // Even if a client id is passed in Development we must not force a
        // ManagedIdentity-only path; the developer's az/VSCode credential
        // has to remain reachable.
        var factory = new AzureCredentialFactory(HostEnv(Environments.Development));

        var credential = factory.CreateCredential("11111111-2222-3333-4444-555555555555");

        credential.Should().BeOfType<DefaultAzureCredential>();
    }

    [Fact]
    public void CreateCredential_InProduction_WithClientId_ReturnsManagedIdentityCredential()
    {
        // Production short-circuit per the audit (Fix #3) — skips the
        // DefaultAzureCredential chain probe by going straight to IMDS with
        // the explicit UAMI client id.
        var factory = new AzureCredentialFactory(HostEnv(Environments.Production));

        var credential = factory.CreateCredential("11111111-2222-3333-4444-555555555555");

        credential.Should().BeOfType<ManagedIdentityCredential>();
    }

    [Fact]
    public void CreateCredential_InProduction_WithoutClientId_ReturnsDefaultAzureCredential()
    {
        // No UAMI client id wired — fall through to the chain so the service
        // can still boot. The deployment pipeline is responsible for setting
        // AZURE_CLIENT_ID; missing plumbing surfaces in logs as a chain probe
        // rather than a hard crash.
        var factory = new AzureCredentialFactory(HostEnv(Environments.Production));

        var credential = factory.CreateCredential();

        credential.Should().BeOfType<DefaultAzureCredential>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateCredential_InProduction_WhitespaceClientId_FallsBackToDefault(string clientId)
    {
        var factory = new AzureCredentialFactory(HostEnv(Environments.Production));

        var credential = factory.CreateCredential(clientId);

        credential.Should().BeOfType<DefaultAzureCredential>();
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
