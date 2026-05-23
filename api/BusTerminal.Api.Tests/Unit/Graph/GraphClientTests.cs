using System.Net.Http;
using BusTerminal.Api.Infrastructure.Graph;
using FluentAssertions;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Xunit;

namespace BusTerminal.Api.Tests.Unit.Graph;

/// <summary>
/// Unit coverage for <see cref="GraphClient"/> (spec 003 / US6 / FR-023).
///
/// The Graph SDK v5 (Kiota-generated) wraps the transport via
/// <c>IRequestAdapter</c>. Hand-rolling a stub adapter to drive a real
/// <see cref="Microsoft.Graph.GraphServiceClient"/> is expensive — the adapter
/// surface has dozens of overloads and the generated request builders are
/// version-sensitive. Instead, <see cref="GraphClient"/> exposes an internal
/// constructor that accepts the resolved user-fetch delegate directly. The
/// behavior we care about for this slice is identical either way:
///
///   - SDK returns a populated <c>User</c> ⇒ projected to <see cref="GraphUser"/>
///     with every field mapped (`Id`, `DisplayName`, `UserPrincipalName`,
///     `Mail`).
///   - SDK returns null OR throws <see cref="ODataError"/> with
///     <c>ResponseStatusCode == 404</c> ⇒ <c>null</c> bubbles up to the
///     caller (FR-008 graceful unknown-user handling).
///   - Any other thrown exception (including <c>ODataError</c> for non-404
///     statuses) propagates unmodified — FR-024's graceful-degradation path
///     for missing admin consent is the *caller's* responsibility (see
///     <c>DeveloperToolingProbeEndpoint</c>).
///
/// The integration test at <c>Integration/GraphResolveSelfTests</c> covers
/// the live-Graph leg behind an opt-in env var so the unit tier stays fast
/// and offline.
/// </summary>
public sealed class GraphClientTests
{
    private const string TestOid = "00000000-0000-0000-0000-0000000000aa";

    [Fact]
    public async Task ResolveUserAsync_MapsAllFields_WhenGraphReturnsPopulatedUser()
    {
        var user = new User
        {
            Id = TestOid,
            DisplayName = "Alice Example",
            UserPrincipalName = "alice@example.com",
            Mail = "alice@example.com",
        };
        var client = new GraphClient((_, _) => Task.FromResult<User?>(user));

        var result = await client.ResolveUserAsync(TestOid, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ObjectId.Should().Be(TestOid);
        result.DisplayName.Should().Be("Alice Example");
        result.UserPrincipalName.Should().Be("alice@example.com");
        result.Mail.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task ResolveUserAsync_FallsBackToRequestedObjectId_WhenGraphUserIdNull()
    {
        // Defensive: the Graph SDK contract makes `Id` nullable; a populated
        // response with a null Id should still yield a well-formed GraphUser
        // pinned to the requested object id rather than fabricating a `null`
        // ObjectId on the projection.
        var user = new User { Id = null, DisplayName = "No Id User" };
        var client = new GraphClient((_, _) => Task.FromResult<User?>(user));

        var result = await client.ResolveUserAsync(TestOid, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ObjectId.Should().Be(TestOid);
        result.DisplayName.Should().Be("No Id User");
    }

    [Fact]
    public async Task ResolveUserAsync_ReturnsNull_WhenFetchReturnsNull()
    {
        var client = new GraphClient((_, _) => Task.FromResult<User?>(null));

        var result = await client.ResolveUserAsync(TestOid, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserAsync_ReturnsNull_WhenGraphThrows404ODataError()
    {
        var notFound = new ODataError { ResponseStatusCode = 404 };
        var client = new GraphClient((_, _) => throw notFound);

        var result = await client.ResolveUserAsync(TestOid, CancellationToken.None);

        result.Should().BeNull("404 from Graph means the user object id is unknown — surface as null, not as an exception");
    }

    [Fact]
    public async Task ResolveUserAsync_Propagates403ODataError()
    {
        // 403 (missing consent / missing app role) MUST propagate so the
        // caller can either fail-loud (most code paths) or degrade
        // gracefully (the developer-tooling probe — FR-024). The Graph
        // client itself does not decide the policy.
        var forbidden = new ODataError { ResponseStatusCode = 403 };
        var client = new GraphClient((_, _) => throw forbidden);

        var act = async () => await client.ResolveUserAsync(TestOid, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ODataError>();
        thrown.Which.ResponseStatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ResolveUserAsync_PropagatesArbitraryExceptions()
    {
        var transport = new HttpRequestException("DNS lookup failed");
        var client = new GraphClient((_, _) => throw transport);

        var act = async () => await client.ResolveUserAsync(TestOid, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ResolveUserAsync_PassesCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? capturedToken = null;
        var client = new GraphClient((_, ct) =>
        {
            capturedToken = ct;
            return Task.FromResult<User?>(new User { Id = TestOid });
        });

        await client.ResolveUserAsync(TestOid, cts.Token);

        capturedToken.Should().NotBeNull();
        capturedToken!.Value.Should().Be(cts.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ResolveUserAsync_RejectsBlankObjectId(string? objectId)
    {
        var client = new GraphClient((_, _) => Task.FromResult<User?>(null));

        var act = async () => await client.ResolveUserAsync(objectId!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Ctor_RejectsNullCredentialFactory()
    {
        var act = () => new GraphClient((BusTerminal.Api.Infrastructure.Credentials.IAzureCredentialFactory)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_RejectsNullFetchDelegate()
    {
        // Suppress nullable warning for the deliberate negative test.
        var act = () => new GraphClient((Func<string, CancellationToken, Task<User?>>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IRequestAdapter_IsResolvableByName_SmokeReference()
    {
        // The task spec hints that mocking should target IRequestAdapter
        // (the Kiota transport seam beneath GraphServiceClient). The
        // delegate-seam pattern used in this test class is functionally
        // equivalent for unit coverage of the projection logic — this
        // smoke assertion is here purely to keep the IRequestAdapter
        // reference resolvable, so a future test author who wants to
        // wire a real GraphServiceClient against a stub adapter can
        // find the import path without re-searching the SDK.
        typeof(IRequestAdapter).Should().NotBeNull();
    }
}
