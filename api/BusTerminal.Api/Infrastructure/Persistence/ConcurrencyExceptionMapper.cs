using System.Net;
using BusTerminal.Api.Domain;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-025 / Q2. Translates the SDK's 412 PreconditionFailed (a stale
// ETag on an IfMatch write) into a domain-typed exception callers can catch
// without referencing the Cosmos SDK directly.
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(
        ResourceId resourceId,
        ConcurrencyToken presentedToken,
        ConcurrencyToken? currentToken,
        Exception? innerException = null)
        : base(
            $"Concurrency conflict on resource {resourceId}. " +
            $"Presented ETag: '{presentedToken}', current: '{currentToken?.ToString() ?? "<unknown>"}'.",
            innerException)
    {
        ResourceId = resourceId;
        PresentedToken = presentedToken;
        CurrentToken = currentToken;
    }

    public ResourceId ResourceId { get; }

    public ConcurrencyToken PresentedToken { get; }

    public ConcurrencyToken? CurrentToken { get; }
}

public static class ConcurrencyExceptionMapper
{
    public static bool TryMap(
        CosmosException cosmosException,
        ResourceId resourceId,
        ConcurrencyToken presentedToken,
        out ConcurrencyConflictException? domainException)
    {
        ArgumentNullException.ThrowIfNull(cosmosException);

        if (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            domainException = new ConcurrencyConflictException(
                resourceId,
                presentedToken,
                currentToken: null, // Cosmos does not return the current ETag on 412.
                innerException: cosmosException);
            return true;
        }

        domainException = null;
        return false;
    }
}
