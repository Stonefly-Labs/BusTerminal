using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using BusTerminal.Api.Infrastructure.Credentials;
using BusTerminal.Api.Infrastructure.Graph;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BusTerminal.Api.Tests.Integration;

/// <summary>
/// Spec 003 / US6 / SC-009 — verifies the app-only Graph flow round-trips
/// against the real dev tenant. The test is opt-in via the
/// <c>BUSTERMINAL_GRAPH_INTEGRATION=1</c> environment variable so CI and
/// offline-developer runs skip it cleanly; turn it on locally after
/// <c>az login --tenant 596c1564-6e95-4c35-a80b-2dbe45a162f3</c> (or after
/// granting the API MI's identity admin consent in a deployed env).
///
/// What this test proves end-to-end:
///   - <see cref="AzureCredentialFactory"/> + <see cref="GraphClient"/>
///     compose without surprise — same code path used at runtime.
///   - <c>User.Read.All</c> admin consent is in place for the configured
///     tenant (a Graph 403 here is the "consent missing" signal).
///   - The Graph SDK returns a usable <see cref="GraphUser"/> for a
///     real object id with a non-empty <c>displayName</c>.
///
/// Gating env vars:
///   <c>BUSTERMINAL_GRAPH_INTEGRATION=1</c>       — opt-in switch
///   <c>BUSTERMINAL_GRAPH_TEST_OID</c>            — object id to resolve
///                                                 (defaults to the signed-in
///                                                 az caller's oid if unset
///                                                 and obtainable)
/// </summary>
public sealed class GraphResolveSelfTests
{
    private const string OptInEnvVar = "BUSTERMINAL_GRAPH_INTEGRATION";
    private const string TestOidEnvVar = "BUSTERMINAL_GRAPH_TEST_OID";

    [Fact]
    public async Task ResolveUserAsync_ReturnsDisplayName_ForKnownTenantUser()
    {
        // Skip cleanly when the opt-in flag isn't set. xUnit's preferred
        // "soft skip" pattern is Skip.If from a separate assertion lib;
        // since we don't ship that, an early return is fine — the test
        // surface stays green in CI without false-pass noise.
        if (!IsOptedIn())
        {
            return;
        }

        var oid = Environment.GetEnvironmentVariable(TestOidEnvVar);
        oid.Should().NotBeNullOrWhiteSpace(
            $"set {TestOidEnvVar} to the object id of any user in the BusTerminal dev tenant " +
            "(your own `az ad signed-in-user show --query id -o tsv` works)");

        var factory = new AzureCredentialFactory(HostEnv(Environments.Development));
        var graphClient = new GraphClient(factory);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await graphClient.ResolveUserAsync(oid!, cts.Token);

        result.Should().NotBeNull(
            "Graph returned no user — either the oid is wrong, consent has not been granted, or the caller cannot reach Graph");
        result!.DisplayName.Should().NotBeNullOrWhiteSpace(
            "User.Read.All is supposed to return at least a display name for any tenant user");
    }

    private static bool IsOptedIn() =>
        string.Equals(Environment.GetEnvironmentVariable(OptInEnvVar), "1", StringComparison.Ordinal);

    private static IHostEnvironment HostEnv(string environmentName) =>
        new IntegrationHostEnvironment(environmentName);

    private sealed class IntegrationHostEnvironment : IHostEnvironment
    {
        public IntegrationHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "BusTerminal.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
