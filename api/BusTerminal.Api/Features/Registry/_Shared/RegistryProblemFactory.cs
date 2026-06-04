using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / contracts/registry-api.yaml #/components/schemas/Problem. RFC-7807
// problem document helpers for the registry slice. Centralizes the URI
// namespace and the standard payload shape so endpoint handlers produce
// consistent error bodies.
internal static class RegistryProblemFactory
{
    public const string ProblemBaseUri = "https://busterminal.dev/probs/";
    public const string ProblemContentType = "application/problem+json";

    public static IResult ValidationProblem(ValidationResult validation, string? instance = null)
    {
        var errors = validation.Errors
            .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
            .ToArray();

        return Results.Json(new
        {
            type = ProblemBaseUri + "validation-failed",
            title = "Validation failed",
            status = 400,
            code = "ValidationFailed",
            errors,
            instance,
        },
        statusCode: 400,
        contentType: ProblemContentType);
    }

    public static IResult NotFound(string code, string title, string detail, string? instance = null) =>
        Results.Json(new
        {
            type = ProblemBaseUri + code.ToLowerInvariant(),
            title,
            status = 404,
            code,
            detail,
            instance,
        },
        statusCode: 404,
        contentType: ProblemContentType);

    public static IResult Conflict(string code, string title, string detail, string? instance = null) =>
        Results.Json(new
        {
            type = ProblemBaseUri + code.ToLowerInvariant(),
            title,
            status = 409,
            code,
            detail,
            instance,
        },
        statusCode: 409,
        contentType: ProblemContentType);

    public static IResult BadRequest(string code, string title, string detail, string? instance = null) =>
        Results.Json(new
        {
            type = ProblemBaseUri + code.ToLowerInvariant(),
            title,
            status = 400,
            code,
            detail,
            instance,
        },
        statusCode: 400,
        contentType: ProblemContentType);

    public static IResult PreconditionRequired(string detail, string? instance = null) =>
        Results.Json(new
        {
            type = ProblemBaseUri + "if-match-required",
            title = "If-Match header required",
            status = 428,
            code = "IfMatchRequired",
            detail,
            instance,
        },
        statusCode: 428,
        contentType: ProblemContentType);
}
