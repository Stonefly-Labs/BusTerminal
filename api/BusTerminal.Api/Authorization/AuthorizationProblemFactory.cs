using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;

namespace BusTerminal.Api.Authorization;

/// <summary>
/// RFC 7807 problem-details body returned for 403 results from BusTerminal
/// role-policy authorization. Shape matches the AuthorizationProblem schema in
/// <c>specs/003-auth-and-identity/contracts/role-probes.openapi.yaml</c>.
///
/// The caller's effective roles are deliberately omitted to avoid leaking
/// authorization posture back to the caller (FR-033 / contract design note).
/// </summary>
public sealed record AuthorizationProblem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("requiredOperationClass")] string RequiredOperationClass,
    [property: JsonPropertyName("requiredRoles")] IReadOnlyList<string> RequiredRoles,
    [property: JsonPropertyName("correlationId")] string CorrelationId);

internal static class AuthorizationProblemFactory
{
    public const string ProblemTypeUri = "https://busterminal/problems/authorization/insufficient-role";
    public const string ProblemTitle = "Insufficient role";
    public const string ProblemDetail = "The calling principal does not hold any role authorized for the requested operation class.";
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Resolves the operation class and authorized role set for the policy that
    /// gated the current endpoint. Reads the endpoint's <see cref="IAuthorizeData"/>
    /// metadata to find the named operation-class policy, then maps that policy
    /// name back to the matrix entry in <see cref="RolePolicyMatrix"/>.
    ///
    /// Returns null when no BusTerminal operation-class policy was applied — in
    /// which case the caller falls back to the default forbid response.
    /// </summary>
    public static AuthorizationPolicyMetadata? ResolveMetadata(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            return null;
        }

        foreach (var authorizeData in endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>())
        {
            var policyName = authorizeData.Policy;
            if (string.IsNullOrEmpty(policyName))
            {
                continue;
            }

            if (RolePolicyMatrix.TryResolve(policyName, out var operationClass, out var roles))
            {
                return new AuthorizationPolicyMetadata(operationClass, roles);
            }
        }

        return null;
    }

    public static async Task WriteAsync(HttpContext context, AuthorizationPolicyMetadata metadata)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = ContentType;
        var correlationId = Activity.Current?.TraceId.ToHexString() ?? string.Empty;
        var problem = new AuthorizationProblem(
            Type: ProblemTypeUri,
            Title: ProblemTitle,
            Status: StatusCodes.Status403Forbidden,
            Detail: ProblemDetail,
            RequiredOperationClass: metadata.OperationClass.ToString(),
            RequiredRoles: metadata.RequiredRoles,
            CorrelationId: correlationId);
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions);
    }
}

internal sealed record AuthorizationPolicyMetadata(
    OperationClass OperationClass,
    IReadOnlyList<string> RequiredRoles);

/// <summary>
/// Authoritative mapping from operation-class policy name → operation class
/// + authorized role set. Mirrored from <see cref="RolePolicies"/>; the two
/// MUST stay in sync. A test in <c>RolePoliciesTests</c> asserts this.
/// </summary>
internal static class RolePolicyMatrix
{
    private static readonly Dictionary<string, (OperationClass Class, string[] Roles)> Entries =
        new(StringComparer.Ordinal)
        {
            [OperationClassPolicies.CanRead] = (OperationClass.Read, new[]
            {
                PlatformRoleClaims.Admin,
                PlatformRoleClaims.Developer,
                PlatformRoleClaims.Operator,
                PlatformRoleClaims.Reader,
            }),
            [OperationClassPolicies.CanMutateDomain] = (OperationClass.MutateDomain, new[]
            {
                PlatformRoleClaims.Admin,
                PlatformRoleClaims.Operator,
            }),
            [OperationClassPolicies.CanOperatePlatform] = (OperationClass.OperatePlatform, new[]
            {
                PlatformRoleClaims.Admin,
                PlatformRoleClaims.Operator,
            }),
            [OperationClassPolicies.CanAdminister] = (OperationClass.Administer, new[]
            {
                PlatformRoleClaims.Admin,
            }),
            [OperationClassPolicies.CanUseDeveloperTooling] = (OperationClass.DeveloperTooling, new[]
            {
                PlatformRoleClaims.Admin,
                PlatformRoleClaims.Developer,
            }),
        };

    public static bool TryResolve(string policyName, out OperationClass operationClass, out IReadOnlyList<string> roles)
    {
        if (Entries.TryGetValue(policyName, out var entry))
        {
            operationClass = entry.Class;
            roles = entry.Roles;
            return true;
        }
        operationClass = default;
        roles = Array.Empty<string>();
        return false;
    }
}
