namespace BusTerminal.Api.Features.Discovery.StartDiscovery;

// Spec 009 / T044. The POST /api/namespaces/{namespaceId}/discover endpoint
// takes no request body — `namespaceId` comes from the route and `requestedBy`
// is derived from the authenticated principal. We model an empty record so
// the FluentValidation surface stays uniform with the rest of the API.
public sealed record StartDiscoveryRequest;
