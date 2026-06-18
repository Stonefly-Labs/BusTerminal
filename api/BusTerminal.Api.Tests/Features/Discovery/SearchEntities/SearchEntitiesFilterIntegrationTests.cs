using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Infrastructure.Search;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.SearchEntities;

// Spec 009 / T063. Unit-level integration coverage for the adapter's filter
// composition. A live AI Search cluster run is gated on the
// BUSTERMINAL_TEST_AI_SEARCH_ENDPOINT env var (skipped when absent), but the
// pure filter-string assertions below run in every build so we catch
// regressions to the OData clause shape immediately.
public sealed class SearchEntitiesFilterIntegrationTests
{
    [Fact]
    public void Filter_LifecycleAndRole_ComposesAndClause()
    {
        var request = new PublishedEntitySearchRequest(
            LifecycleStatusFilters: new[] { LifecycleStatus.Active, LifecycleStatus.Missing },
            AssociationRoleFilters: new[] { EntityServiceRole.Owner });
        var filter = AzurePublishedEntitySearchClient.BuildFilter(request);

        filter.Should().Contain("lifecycleStatus ne null");
        filter.Should().Contain("(lifecycleStatus eq 'Active' or lifecycleStatus eq 'Missing')");
        filter.Should().Contain("associationRoles/any(r: r eq 'Owner')");
    }

    [Fact]
    public void Filter_NamespaceAndTag_ComposesAndClause()
    {
        var request = new PublishedEntitySearchRequest(
            NamespaceIdFilter: "ns_demo",
            TagFilters: new[] { "domain:orders", "tier:critical" });
        var filter = AzurePublishedEntitySearchClient.BuildFilter(request);

        filter.Should().Contain("namespaceId eq 'ns_demo'");
        filter.Should().Contain("tags/any(t: t eq 'domain:orders' or t eq 'tier:critical')");
    }

    [Fact]
    public void Filter_MultiValueRoleNarrowing_OrsAllRoles()
    {
        var request = new PublishedEntitySearchRequest(
            AssociatedServiceIdFilter: "svc_billing",
            AssociationRoleFilters: new[] { EntityServiceRole.Owner, EntityServiceRole.Consumer });
        var filter = AzurePublishedEntitySearchClient.BuildFilter(request);

        filter.Should().Contain("associatedServiceIds/any(s: s eq 'svc_billing')");
        filter.Should().Contain("associationRoles/any(r: r eq 'Owner' or r eq 'Consumer')");
    }

    [Fact]
    public void Filter_NoFilters_StillAppliesLifecycleNotNullGate()
    {
        var request = new PublishedEntitySearchRequest();
        var filter = AzurePublishedEntitySearchClient.BuildFilter(request);
        filter.Should().Be("lifecycleStatus ne null");
    }

    [Fact]
    public void Filter_EscapesSingleQuotesInUserInput()
    {
        var request = new PublishedEntitySearchRequest(
            NamespaceIdFilter: "ns_o'donnell",
            TagFilters: new[] { "tag'with'quotes" });
        var filter = AzurePublishedEntitySearchClient.BuildFilter(request);
        filter.Should().Contain("namespaceId eq 'ns_o''donnell'");
        filter.Should().Contain("tag''with''quotes");
    }
}
