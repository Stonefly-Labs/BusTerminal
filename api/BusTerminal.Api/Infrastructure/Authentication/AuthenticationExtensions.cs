using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace BusTerminal.Api.Infrastructure.Authentication;

public static class AuthenticationExtensions
{
    public const string DevelopmentTenantSentinel = "development";
    public const string RequireAuthenticatedUserPolicy = "RequireAuthenticatedUser";

    public static IServiceCollection AddBusTerminalAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var azureAdSection = configuration.GetSection("AzureAd");
        var tenantId = azureAdSection["TenantId"];

        if (string.Equals(tenantId, DevelopmentTenantSentinel, StringComparison.Ordinal))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "AzureAd:TenantId is set to 'development' outside the Development environment. " +
                    "The mock authentication handler is gated to Development only.");
            }

            services
                .AddAuthentication(MockAuthenticationHandler.SchemeName)
                .AddScheme<MockAuthenticationOptions, MockAuthenticationHandler>(
                    MockAuthenticationHandler.SchemeName,
                    _ => { });
        }
        else
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(azureAdSection);
        }

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                RequireAuthenticatedUserPolicy,
                policy => policy.RequireAuthenticatedUser())
            .SetDefaultPolicy(
                new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());

        return services;
    }
}
