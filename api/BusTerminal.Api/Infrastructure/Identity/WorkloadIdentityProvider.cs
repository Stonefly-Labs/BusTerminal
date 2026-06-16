using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Infrastructure.Identity;

// Spec 008 / research §17 + outputs-contract.md §1.4. The workload UAMI's
// principalId is injected at deploy time via the WORKLOAD_PRINCIPAL_ID
// environment variable (sourced from `module.workload_identity.principal_id`
// in IaC). Parsed once at startup, cached for the process lifetime,
// exposed to the `/api/namespaces/identity` endpoint and the validation
// runner's IdentityAuthorization check.
//
// Graph `/me` is NOT used here — `/me` is delegated-flow-only and returns
// 401/404 under application-token flows. Direct env-var injection is the
// cleanest path: zero Graph dependency on cold start, value is already
// known to OpenTofu at apply time.
//
// On missing or unparseable value: a structured ERROR log fires once at
// resolution time and GetPrincipalIdAsync surfaces an InvalidOperationException
// — the endpoint layer maps this to a 500 ProblemDetails so the operator can
// see the deployment misconfiguration immediately.
public sealed partial class WorkloadIdentityProvider
{
    public const string ConfigurationKey = "WORKLOAD_PRINCIPAL_ID";

    private readonly Lazy<Guid> _principalId;
    private readonly ILogger<WorkloadIdentityProvider> _logger;

    public WorkloadIdentityProvider(IConfiguration configuration, ILogger<WorkloadIdentityProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger;
        _principalId = new Lazy<Guid>(() => Resolve(configuration[ConfigurationKey]));
    }

    public Task<Guid> GetPrincipalIdAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_principalId.Value);
    }

    private Guid Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            LogMissing();
            throw new InvalidOperationException(
                $"{ConfigurationKey} configuration value is missing. The workload UAMI principalId must be injected at deploy time per spec 008 research §17.");
        }

        if (!Guid.TryParse(raw, out var parsed))
        {
            LogUnparseable();
            throw new InvalidOperationException(
                $"{ConfigurationKey} configuration value is not a valid Guid.");
        }

        return parsed;
    }

    [LoggerMessage(EventId = 8401, Level = LogLevel.Error, Message = "WORKLOAD_PRINCIPAL_ID environment variable is missing — the namespace-identity endpoint cannot serve the workload UAMI principalId.")]
    private partial void LogMissing();

    [LoggerMessage(EventId = 8402, Level = LogLevel.Error, Message = "WORKLOAD_PRINCIPAL_ID environment variable is not a valid Guid.")]
    private partial void LogUnparseable();
}
