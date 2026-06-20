using System.Text.Json;
using System.Text.Json.Serialization;
using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using BusTerminal.Api.Features.Discovery.UpdateEntityMetadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace BusTerminal.Api.Features.Discovery.ServiceAssociations;

// Spec 009 / T107 + contracts/openapi.yaml#addEntityAssociation.
// POST /api/entities/{entityId}/associations
//
// Adds a single (serviceId, role) association to the entity. ETag-enforced;
// duplicate triples surface as 409 ConcurrencyConflict. Authorization
// follows R-15's three-branch path (Admin | NamespaceAdministrator |
// Service-Owner). New associationId minted server-side via ULID-style
// random base32 — same shape the worker uses when seeding.
public static class AddAssociationEndpoint
{
    public static IEndpointRouteBuilder MapAddAssociationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapPost("/api/entities/{entityId}/associations", HandleAsync)
            .RequireAuthorization()
            .WithName("AddEntityAssociation")
            .WithTags("Associations");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string entityId,
        IPublishedEntitySearchClient searchClient,
        IPublishedEntityStore entityStore,
        EntityMetadataEditorAuthorizer authorizer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidEntityId", "entityId is required.");
        }
        if (!context.Request.Headers.TryGetValue("If-Match", out var ifMatch) || string.IsNullOrWhiteSpace(ifMatch))
        {
            return Results.Problem(
                title: "IfMatchRequired",
                detail: "If-Match header is required.",
                statusCode: StatusCodes.Status428PreconditionRequired,
                instance: context.Request.Path,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "IfMatchRequired" });
        }

        AddAssociationRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<AddAssociationRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidJson", $"Body did not deserialize: {ex.Message}");
        }
        if (request is null || string.IsNullOrWhiteSpace(request.ServiceId))
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidBody", "serviceId is required.");
        }
        if (request.Role is null)
        {
            return UpdateEntityMetadataEndpoint.BadRequest(context, "InvalidRole", "role is required and must be Owner|Producer|Consumer.");
        }

        var searchResults = await searchClient.SearchAsync(
            new PublishedEntitySearchRequest(Query: entityId, Skip: 0, Top: 5),
            cancellationToken).ConfigureAwait(false);
        var hit = searchResults.Hits.FirstOrDefault(h => string.Equals(h.Id, entityId, StringComparison.Ordinal));
        if (hit is null || string.IsNullOrEmpty(hit.Environment))
        {
            return UpdateEntityMetadataEndpoint.NotFound(context, entityId);
        }

        var current = await entityStore.GetDetailAsync(entityId, hit.Environment, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return UpdateEntityMetadataEndpoint.NotFound(context, entityId);
        }

        var authResult = await authorizer.AuthorizeAsync(
            context.User, entityId, current.Entity.ServiceAssociations, cancellationToken).ConfigureAwait(false);
        if (!authResult.Allowed)
        {
            return UpdateEntityMetadataEndpoint.Forbidden(context, entityId);
        }

        var modifiedBy = UpdateEntityMetadataEndpoint.ResolveModifiedBy(context);
        var association = new EntityServiceAssociation(
            AssociationId: MintAssociationId(),
            ServiceId: request.ServiceId.Trim(),
            Role: request.Role.Value,
            CreatedUtc: DateTimeOffset.UtcNow,
            CreatedBy: modifiedBy);

        try
        {
            var created = await entityStore.AddAssociationAsync(
                entityId, hit.Environment, association, ifMatch.ToString(), modifiedBy, cancellationToken).ConfigureAwait(false);
            context.Response.Headers[HeaderNames.ETag] = created.Detail.ETag;
            return Results.Created($"/api/entities/{entityId}/associations/{created.Association.AssociationId}", created.Association);
        }
        catch (DuplicateServiceAssociationException ex)
        {
            return UpdateEntityMetadataEndpoint.Conflict(context, "DuplicateAssociation",
                $"An association ({ex.ServiceId}, {ex.Role}) already exists on entity {entityId}.");
        }
        catch (PublishedEntityConcurrencyConflictException)
        {
            return UpdateEntityMetadataEndpoint.PreconditionFailed(context, entityId);
        }
        catch (PublishedEntityNotFoundException)
        {
            return UpdateEntityMetadataEndpoint.NotFound(context, entityId);
        }
    }

    internal static string MintAssociationId()
    {
        // 16 random bytes → 26-char base32 (Crockford). Prefix `esa_` per
        // data-model.md §4. Cryptographic randomness for uniqueness; not a
        // monotonic ULID — endpoint callers should not rely on ordering.
        Span<byte> bytes = stackalloc byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return "esa_" + Base32Crockford.Encode(bytes);
    }
}

public sealed record AddAssociationRequest(
    [property: JsonPropertyName("serviceId")] string ServiceId,
    [property: JsonPropertyName("role")] EntityServiceRole? Role);

// Spec 009 / T107. Crockford base32 (no padding) encoder. Used for the
// `esa_` association identifiers — readable, URL-safe, no ambiguous chars.
internal static class Base32Crockford
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;
        var bitCount = bytes.Length * 8;
        var outLen = (bitCount + 4) / 5;
        var result = new char[outLen];
        int bitBuffer = 0;
        int bitsInBuffer = 0;
        int outIndex = 0;
        foreach (var b in bytes)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                var ix = (bitBuffer >> bitsInBuffer) & 0x1F;
                result[outIndex++] = Alphabet[ix];
            }
        }
        if (bitsInBuffer > 0)
        {
            var ix = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
            result[outIndex++] = Alphabet[ix];
        }
        return new string(result, 0, outIndex);
    }
}
