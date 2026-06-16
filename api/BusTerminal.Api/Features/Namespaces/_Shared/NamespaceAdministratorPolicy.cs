using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / research §5 + §15. Endpoint-level filter that applies the
// `CanAdministerNamespaces` policy to namespace write surfaces. Built as a
// reusable filter (not duplicated on every endpoint) so the rejection log +
// span event are emitted in one place per FR-035.
//
// Reads remain AuthN-only (the wider tenant population can browse / inspect
// namespaces); writes require the namespace-administrator role.
public static partial class NamespaceAdministratorPolicy
{
    public const string PolicyName = NamespacePolicies.CanAdministerNamespaces;

    private const string LoggerCategory = "BusTerminal.NamespaceAdministratorPolicy";

    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Warning,
        Message = "namespace-administrator role required for {Method} {Path}")]
    private static partial void LogRoleMissing(ILogger logger, string method, string path);

    // Attach via `.RequireNamespaceAdministrator()` on a RouteHandlerBuilder
    // or a RouteGroupBuilder. Composes with the AuthN gate set up by the
    // shared NamespaceEndpointsBuilder.MapGroup pattern.
    public static TBuilder RequireNamespaceAdministrator<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.RequireAuthorization(PolicyName);
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var authResult = await http.AuthenticateAsync().ConfigureAwait(false);
            if (authResult.Succeeded
                && authResult.Principal is not null
                && authResult.Principal.GetEffectiveRoles().Contains(PlatformRole.NamespaceAdministrator))
            {
                return await next(context).ConfigureAwait(false);
            }

            var logger = http.RequestServices.GetService<ILoggerFactory>()
                ?.CreateLogger(LoggerCategory);
            if (logger is not null)
            {
                LogRoleMissing(logger, http.Request.Method, http.Request.Path.Value ?? string.Empty);
            }

            var activity = System.Diagnostics.Activity.Current;
            activity?.AddEvent(new System.Diagnostics.ActivityEvent(
                "namespace.authorization.rejected",
                tags: new System.Diagnostics.ActivityTagsCollection
                {
                    { "http.method", http.Request.Method },
                    { "http.route", http.Request.Path.Value },
                }));

            return Results.Problem(
                title: "Forbidden",
                detail: "Requires the namespace-administrator role.",
                statusCode: StatusCodes.Status403Forbidden,
                instance: http.Request.Path);
        });
        return builder;
    }
}
