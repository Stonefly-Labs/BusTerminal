using Azure;
using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Tests.Features.Registry.Fakes;

// Spec 006 / Phase 4 US2. Programmable in-memory ISearchClient used by the
// SearchEndpoint contract tests. Tests assign `NextResults` (happy path) or
// `ThrowOnSearch` (503 path) before issuing the HTTP request.
public sealed class FakeSearchClient : ISearchClient
{
    public RegistrySearchResults NextResults { get; set; } = new(Array.Empty<RegistrySearchHit>(), 0);
    public RegistrySearchRequest? LastRequest { get; private set; }
    public bool ThrowOnSearch { get; set; }
    public int ThrowStatus { get; set; } = 503;

    public Task<RegistrySearchResults> SearchAsync(RegistrySearchRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (ThrowOnSearch)
        {
            throw new RequestFailedException(ThrowStatus, "AI Search is unavailable", "ServiceUnavailable", innerException: null);
        }
        return Task.FromResult(NextResults);
    }

    public Task<IReadOnlyList<RegistrySuggestion>> SuggestAsync(
        string partialText,
        int top,
        string? environmentFilter,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RegistrySuggestion>>(Array.Empty<RegistrySuggestion>());

    public void Reset()
    {
        NextResults = new RegistrySearchResults(Array.Empty<RegistrySearchHit>(), 0);
        LastRequest = null;
        ThrowOnSearch = false;
    }
}
