using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Infrastructure.Graph;
using FluentAssertions;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Xunit;

namespace BusTerminal.Api.Tests.Infrastructure.Graph;

// Spec 008 / T036. Matches the GraphClient unit-test convention — delegate
// seams stand in for the Graph SDK v5 Kiota request adapter so the projection
// + ordering + cap logic is covered without stubbing IRequestAdapter.
public sealed class GraphPrincipalPickerTests
{
    private static readonly Guid AliceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GroupAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyResult()
    {
        var picker = NewPicker(
            users: (_, _, _) => throw new InvalidOperationException("must not call Graph for empty query"),
            groups: (_, _, _) => throw new InvalidOperationException("must not call Graph for empty query"));

        var result = await picker.SearchAsync("  ", top: 25, includeGroups: true, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_UserOnlySearch_ReturnsUsersOrderedByDisplayName()
    {
        var picker = NewPicker(
            users: (_, _, _) => Task.FromResult<UserCollectionResponse?>(new UserCollectionResponse
            {
                Value = new List<User>
                {
                    new() { Id = BobId.ToString("D"), DisplayName = "Bob", UserPrincipalName = "bob@x" },
                    new() { Id = AliceId.ToString("D"), DisplayName = "Alice", UserPrincipalName = "alice@x", Mail = "alice@x" },
                },
            }),
            groups: null);

        var result = await picker.SearchAsync("A", top: 25, includeGroups: false, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].DisplayName.Should().Be("Alice");
        result[0].ObjectId.Should().Be(AliceId);
        result[0].PrincipalType.Should().Be(PrincipalType.User);
        result[1].DisplayName.Should().Be("Bob");
    }

    [Fact]
    public async Task SearchAsync_IncludeGroups_MergesUsersAndGroupsAlphabetically()
    {
        var picker = NewPicker(
            users: (_, _, _) => Task.FromResult<UserCollectionResponse?>(new UserCollectionResponse
            {
                Value = new List<User>
                {
                    new() { Id = AliceId.ToString("D"), DisplayName = "Alice", UserPrincipalName = "alice@x" },
                },
            }),
            groups: (_, _, _) => Task.FromResult<GroupCollectionResponse?>(new GroupCollectionResponse
            {
                Value = new List<Group>
                {
                    new() { Id = GroupAId.ToString("D"), DisplayName = "AcmeOps" },
                },
            }));

        var result = await picker.SearchAsync("A", top: 25, includeGroups: true, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].DisplayName.Should().Be("AcmeOps");
        result[0].PrincipalType.Should().Be(PrincipalType.Group);
        result[1].DisplayName.Should().Be("Alice");
        result[1].PrincipalType.Should().Be(PrincipalType.User);
    }

    [Fact]
    public async Task SearchAsync_CapsResultsToProvidedTop()
    {
        var manyUsers = Enumerable.Range(0, 30)
            .Select(i => new User { Id = Guid.NewGuid().ToString("D"), DisplayName = $"User{i:D2}" })
            .ToList();
        var picker = NewPicker(
            users: (_, top, _) => Task.FromResult<UserCollectionResponse?>(new UserCollectionResponse { Value = manyUsers.Take(top).ToList() }),
            groups: null);

        var result = await picker.SearchAsync("U", top: 25, includeGroups: false, CancellationToken.None);

        result.Should().HaveCount(25, "the picker hard-caps at 25 per research §2");
    }

    [Fact]
    public async Task SearchAsync_EmptyGraphResult_ReturnsEmpty()
    {
        var picker = NewPicker(
            users: (_, _, _) => Task.FromResult<UserCollectionResponse?>(new UserCollectionResponse { Value = new List<User>() }),
            groups: null);

        var result = await picker.SearchAsync("XYZ", top: 25, includeGroups: false, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Graph401_SurfacesAsGraphPickerException()
    {
        var picker = NewPicker(
            users: (_, _, _) => throw new ODataError { ResponseStatusCode = 401 },
            groups: null);

        var act = async () => await picker.SearchAsync("A", top: 25, includeGroups: false, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<GraphPickerException>();
        thrown.Which.InnerException.Should().BeOfType<ODataError>()
            .Which.ResponseStatusCode.Should().Be(401);
    }

    [Fact]
    public async Task SearchAsync_SkipsUsersWithNonGuidId()
    {
        var picker = NewPicker(
            users: (_, _, _) => Task.FromResult<UserCollectionResponse?>(new UserCollectionResponse
            {
                Value = new List<User>
                {
                    new() { Id = "not-a-guid", DisplayName = "Garbage" },
                    new() { Id = AliceId.ToString("D"), DisplayName = "Alice" },
                },
            }),
            groups: null);

        var result = await picker.SearchAsync("A", top: 25, includeGroups: false, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ObjectId.Should().Be(AliceId);
    }

    private static GraphPrincipalPicker NewPicker(
        Func<string, int, CancellationToken, Task<UserCollectionResponse?>> users,
        Func<string, int, CancellationToken, Task<GroupCollectionResponse?>>? groups)
    {
        return new GraphPrincipalPicker(
            users,
            groups ?? ((_, _, _) => Task.FromResult<GroupCollectionResponse?>(new GroupCollectionResponse { Value = new List<Group>() })));
    }
}
