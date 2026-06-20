using System.Security.Claims;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BusTerminal.Api.Tests.Authorization;

// Spec 009 / T024. R-15 three-branch authorization for entity metadata
// edits. The third branch (ServiceOwner-of-Owner-association) is the most
// nuanced — exercised across several seed scenarios.
public sealed class EntityMetadataEditorAuthorizerTests
{
    private static readonly EntityServiceAssociation OwnerAssocSvcA = new(
        AssociationId: "esa_a",
        ServiceId: "svc_a",
        Role: EntityServiceRole.Owner,
        CreatedUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        CreatedBy: "user-x");

    private static readonly EntityServiceAssociation ConsumerAssocSvcB = new(
        AssociationId: "esa_b",
        ServiceId: "svc_b",
        Role: EntityServiceRole.Consumer,
        CreatedUtc: DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
        CreatedBy: "user-x");

    [Fact]
    public async Task Admin_Allowed_ViaAdmin()
    {
        var sut = NewAuthorizer(ownedServiceIds: []);
        var principal = NewPrincipal(PlatformRoleClaims.Admin);
        var result = await sut.AuthorizeAsync(principal, "pe_x", [], CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.Via.Should().Be(EntityEditAuthorizationVia.Admin);
    }

    [Fact]
    public async Task NamespaceAdmin_Allowed_ViaNamespaceAdmin()
    {
        var sut = NewAuthorizer(ownedServiceIds: []);
        var principal = NewPrincipal(PlatformRoleClaims.NamespaceAdministrator);
        var result = await sut.AuthorizeAsync(principal, "pe_x", [], CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.Via.Should().Be(EntityEditAuthorizationVia.NamespaceAdmin);
    }

    [Fact]
    public async Task OwnerOfOwnerAssociatedService_Allowed_ViaServiceOwner()
    {
        var sut = NewAuthorizer(ownedServiceIds: ["svc_a"]);
        var principal = NewPrincipal(PlatformRoleClaims.Reader);
        var result = await sut.AuthorizeAsync(
            principal, "pe_x", [OwnerAssocSvcA, ConsumerAssocSvcB], CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.Via.Should().Be(EntityEditAuthorizationVia.ServiceOwner);
    }

    [Fact]
    public async Task OwnerOfNonOwnerAssociatedService_Denied()
    {
        // User owns svc_b, but svc_b's association is Consumer (not Owner).
        var sut = NewAuthorizer(ownedServiceIds: ["svc_b"]);
        var principal = NewPrincipal(PlatformRoleClaims.Reader);
        var result = await sut.AuthorizeAsync(
            principal, "pe_x", [OwnerAssocSvcA, ConsumerAssocSvcB], CancellationToken.None);

        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task UnrelatedUser_Denied()
    {
        var sut = NewAuthorizer(ownedServiceIds: ["svc_unrelated"]);
        var principal = NewPrincipal(PlatformRoleClaims.Reader);
        var result = await sut.AuthorizeAsync(
            principal, "pe_x", [OwnerAssocSvcA], CancellationToken.None);

        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task UserWithNoOwnedServices_AndNoElevatedRole_Denied()
    {
        var sut = NewAuthorizer(ownedServiceIds: []);
        var principal = NewPrincipal(PlatformRoleClaims.Reader);
        var result = await sut.AuthorizeAsync(
            principal, "pe_x", [OwnerAssocSvcA], CancellationToken.None);

        result.Allowed.Should().BeFalse();
    }

    private static EntityMetadataEditorAuthorizer NewAuthorizer(string[] ownedServiceIds)
        => new(new StubOwnedServicesResolver(ownedServiceIds), NullLogger<EntityMetadataEditorAuthorizer>.Instance);

    private static ClaimsPrincipal NewPrincipal(params string[] roleClaims)
    {
        var identity = new ClaimsIdentity("test");
        foreach (var role in roleClaims)
        {
            identity.AddClaim(new Claim(RolesClaimExtensions.RolesClaimType, role));
        }
        return new ClaimsPrincipal(identity);
    }

    private sealed class StubOwnedServicesResolver(string[] ownedServiceIds) : IOwnedServicesResolver
    {
        public Task<IReadOnlySet<string>> GetOwnedServiceIdsAsync(
            ClaimsPrincipal principal, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(ownedServiceIds, StringComparer.Ordinal));
    }
}
