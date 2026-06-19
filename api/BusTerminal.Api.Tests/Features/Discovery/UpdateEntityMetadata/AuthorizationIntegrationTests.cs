using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Authentication;
using BusTerminal.Api.Tests.Features.Discovery.Shared;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.UpdateEntityMetadata;

// Spec 009 / T098 + R-15. Three-branch authorization for the
// "edit entity metadata" operation:
//   • Platform Admin                       → ALLOW
//   • Namespace Admin                      → ALLOW
//   • Service Owner of an Owner-role assoc → ALLOW
// All other principals (Reader, Service Owner of a Producer/Consumer
// association, unrelated users) → DENY.
public sealed class AuthorizationIntegrationTests : IClassFixture<DiscoveryContractFactory>
{
    private const string EntityId = "pe_DDDDDDDDDDDDDDDDDDDDDDDD";
    private const string OwnerServiceId = "svc_owner_one";
    private const string ProducerServiceId = "svc_producer_one";
    private readonly DiscoveryContractFactory _factory;

    public AuthorizationIntegrationTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
        _factory.PublishedEntitySearch.Reset();
        _factory.OwnedServices.Reset();
    }

    [Fact]
    public async Task PlatformAdmin_IsAllowed()
    {
        Seed("\"etag-1\"", associations: Array.Empty<EntityServiceAssociation>());
        using var client = ClientWithRoles("BusTerminal.Admin");
        var response = await PatchDescriptionAsync(client, "\"etag-1\"", "desc-from-admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NamespaceAdmin_IsAllowed()
    {
        Seed("\"etag-1\"", associations: Array.Empty<EntityServiceAssociation>());
        using var client = ClientWithRoles("BusTerminal.NamespaceAdministrator");
        var response = await PatchDescriptionAsync(client, "\"etag-1\"", "desc-from-ns-admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ServiceOwnerOfOwnerRoleAssociation_IsAllowed()
    {
        Seed("\"etag-1\"", associations: new[]
        {
            new EntityServiceAssociation("esa_owner", OwnerServiceId, EntityServiceRole.Owner,
                DateTimeOffset.UtcNow, "operator"),
        });
        _factory.OwnedServices.Owned.Add(OwnerServiceId);

        using var client = ClientWithRoles("BusTerminal.Reader");
        var response = await PatchDescriptionAsync(client, "\"etag-1\"", "desc-from-service-owner");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ServiceOwnerOfProducerOnlyAssociation_IsDenied()
    {
        Seed("\"etag-1\"", associations: new[]
        {
            new EntityServiceAssociation("esa_producer", ProducerServiceId, EntityServiceRole.Producer,
                DateTimeOffset.UtcNow, "operator"),
        });
        _factory.OwnedServices.Owned.Add(ProducerServiceId);

        using var client = ClientWithRoles("BusTerminal.Reader");
        var response = await PatchDescriptionAsync(client, "\"etag-1\"", "should-not-be-applied");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnrelatedReader_IsDenied()
    {
        Seed("\"etag-1\"", associations: Array.Empty<EntityServiceAssociation>());
        // No services owned.
        using var client = ClientWithRoles("BusTerminal.Reader");
        var response = await PatchDescriptionAsync(client, "\"etag-1\"", "should-not-be-applied");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceOwnerOwningDifferentService_IsDenied()
    {
        Seed("\"etag-1\"", associations: new[]
        {
            new EntityServiceAssociation("esa_owner", OwnerServiceId, EntityServiceRole.Owner,
                DateTimeOffset.UtcNow, "operator"),
        });
        // Caller owns a different service.
        _factory.OwnedServices.Owned.Add("svc_unrelated");

        using var client = ClientWithRoles("BusTerminal.Reader");
        var response = await PatchDescriptionAsync(client, "\"etag-1\"", "should-not-be-applied");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void Seed(string etag, IReadOnlyList<EntityServiceAssociation> associations)
    {
        var (detail, hit) = PublishedEntitySeed.Build(id: EntityId, etag: etag, associations: associations);
        _factory.PublishedEntities.Seed(detail);
        _factory.PublishedEntitySearch.NextResults = new PublishedEntitySearchResults(new[] { hit }, 1);
    }

    private static async Task<HttpResponseMessage> PatchDescriptionAsync(HttpClient client, string ifMatch, string description)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/entities/{EntityId}")
        {
            Content = new StringContent($"{{\"description\":\"{description}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await client.SendAsync(request);
    }

    private HttpClient ClientWithRoles(string rolesHeader)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, rolesHeader);
        return client;
    }
}
