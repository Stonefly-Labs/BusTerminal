using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.ServiceBus;

// Spec 008 / research §1, §3, §14. Concrete ARM probe behind IArmNamespaceProbe.
// Five distinct probe surfaces that map 1:1 to the runner's named checks
// (Existence, Accessibility, RequiredPermissions, IdentityAuthorization,
// ApiReachability — FR-014). Each check:
//
//   - Owns a private timeout via a linked CTS chained off the caller's token.
//     When the linked source fires, the method distinguishes "caller cancelled"
//     from "we timed out" so the categorical reason is correct.
//   - Maps Azure SDK / HTTP failure shapes to the PII-safe ValidationFailureCategory
//     enum + a stable reason string (no raw exception text — FR-035 / data-model
//     §6 "Reason categories"). Unknown failures are mapped to `Unknown` AND emit
//     a structured WARNING log with the exception so App Insights captures the
//     full detail without it ever reaching a span attribute.
//   - Captures the `x-ms-correlation-request-id` response header when available
//     so the runner can stitch ARM-side logs to the validation run.
//
// The probe does NOT start child Activities — that's the runner's job in
// Phase 3 (per research §5). It only reads Activity.Current as ambient
// trace context; SDK calls will create their own client spans which bind
// to the runner's parent automatically.
public sealed partial class ArmNamespaceProbe : IArmNamespaceProbe
{
    private const string ArmManagementClientName = "ArmManagement";
    private const string ArmScope = "https://management.azure.com/.default";
    private const string ArmPermissionsApiVersion = "2022-04-01";
    private const string ServiceBusManagementApiVersion = "2017-04";
    private const string CorrelationRequestIdHeader = "x-ms-correlation-request-id";

    // Reason strings — stable, categorical, surfaced to the UI verbatim.
    private const string ReasonOk = "OK";
    private const string ReasonNotFound = "ArmNamespaceNotFound";
    private const string ReasonAccessDenied = "ArmAccessDenied";
    private const string ReasonReaderRoleMissing = "ReaderRoleMissing";
    private const string ReasonTokenExchangeFailed = "TokenExchangeFailed";
    private const string ReasonApiUnreachable = "ServiceBusManagementUnreachable";
    private const string ReasonThrottled = "ArmThrottled";
    private const string ReasonTimeout = "Timeout";
    private const string ReasonUnknown = "Unknown";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Wildcard tokens that, if present in an `actions` array, are taken to
    // grant Microsoft.ServiceBus/namespaces/read by inclusion. Order is
    // longest-most-specific first; presence of any single token is sufficient.
    private static readonly string[] PermissionGrantingActions =
    {
        "Microsoft.ServiceBus/namespaces/read",
        "Microsoft.ServiceBus/*/read",
        "Microsoft.ServiceBus/*",
        "*/read",
        "*",
    };

    private readonly ArmClient _armClient;
    private readonly IAzureCredentialFactory _credentialFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ArmNamespaceProbeOptions _options;
    private readonly ILogger<ArmNamespaceProbe> _logger;
    private readonly string? _userAssignedClientId;

    public ArmNamespaceProbe(
        ArmClient armClient,
        IAzureCredentialFactory credentialFactory,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IOptions<ArmNamespaceProbeOptions> options,
        ILogger<ArmNamespaceProbe> logger)
    {
        _armClient = armClient;
        _credentialFactory = credentialFactory;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _userAssignedClientId = configuration["AZURE_CLIENT_ID"];
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Probe deliberately maps any unexpected failure to the categorical Unknown reason + structured WARNING log so a single check throwing does not poison the runner's aggregate result.")]
    public async Task<ArmProbeResult> ProbeExistenceAsync(NamespaceArmId armId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(armId);
        _ = Activity.Current; // Ambient correlation; child spans are owned by the runner.

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_options.PerCheckTimeout);

        try
        {
            var response = await GetNamespaceAsync(armId, linked.Token).ConfigureAwait(false);
            var snapshot = BuildSnapshot(response.Value.Data);
            var correlationId = TryReadCorrelationId(response.GetRawResponse());
            return new ArmProbeResult(
                ValidationCheckOutcome.Pass,
                ValidationFailureCategory.Ok,
                ReasonOk,
                correlationId,
                snapshot);
        }
        catch (RequestFailedException ex)
        {
            return MapArmException(ex, includeNotFoundAsFail: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Timeout,
                ReasonTimeout);
        }
        catch (Exception ex)
        {
            LogUnexpectedProbeFailure(ex, nameof(ProbeExistenceAsync), ex.GetType().FullName ?? string.Empty);
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonUnknown);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Probe deliberately maps any unexpected failure to the categorical Unknown reason + structured WARNING log so a single check throwing does not poison the runner's aggregate result.")]
    public async Task<ArmProbeResult> ProbeAccessibilityAsync(NamespaceArmId armId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(armId);
        _ = Activity.Current;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_options.PerCheckTimeout);

        try
        {
            var response = await GetNamespaceAsync(armId, linked.Token).ConfigureAwait(false);
            var correlationId = TryReadCorrelationId(response.GetRawResponse());
            return new ArmProbeResult(
                ValidationCheckOutcome.Pass,
                ValidationFailureCategory.Ok,
                ReasonOk,
                correlationId);
        }
        catch (RequestFailedException ex)
        {
            // Accessibility passes on 404: ARM responded without an auth failure.
            // The Existence check is the authoritative "is the namespace there?"
            // surface; Accessibility is strictly about "did ARM let us talk to it?"
            if (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return new ArmProbeResult(
                    ValidationCheckOutcome.Pass,
                    ValidationFailureCategory.Ok,
                    ReasonOk,
                    TryReadCorrelationId(ex));
            }

            return MapArmException(ex, includeNotFoundAsFail: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Timeout,
                ReasonTimeout);
        }
        catch (Exception ex)
        {
            LogUnexpectedProbeFailure(ex, nameof(ProbeAccessibilityAsync), ex.GetType().FullName ?? string.Empty);
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonUnknown);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Probe deliberately maps any unexpected failure to the categorical Unknown reason + structured WARNING log so a single check throwing does not poison the runner's aggregate result.")]
    public async Task<ArmProbeResult> ProbeRequiredPermissionsAsync(NamespaceArmId armId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(armId);
        _ = Activity.Current;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_options.PerCheckTimeout);

        try
        {
            var token = await AcquireArmTokenAsync(linked.Token).ConfigureAwait(false);
            var httpClient = _httpClientFactory.CreateClient(ArmManagementClientName);
            var uri = new Uri(
                $"https://management.azure.com{armId.CanonicalArmId}/providers/Microsoft.Authorization/permissions?api-version={ArmPermissionsApiVersion}",
                UriKind.Absolute);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);

            var correlationId = TryReadCorrelationId(response);

            if (response.IsSuccessStatusCode)
            {
                var hasReader = await EvaluatePermissionsResponseAsync(response, linked.Token).ConfigureAwait(false);
                if (hasReader)
                {
                    return new ArmProbeResult(
                        ValidationCheckOutcome.Pass,
                        ValidationFailureCategory.Ok,
                        ReasonOk,
                        correlationId);
                }

                return new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.Unauthorized,
                    ReasonReaderRoleMissing,
                    correlationId);
            }

            return MapHttpStatusToProbeResult(response.StatusCode, correlationId);
        }
        catch (CredentialUnavailableException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unauthorized,
                ReasonTokenExchangeFailed);
        }
        catch (AuthenticationFailedException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unauthorized,
                ReasonTokenExchangeFailed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Timeout,
                ReasonTimeout);
        }
        catch (Exception ex)
        {
            LogUnexpectedProbeFailure(ex, nameof(ProbeRequiredPermissionsAsync), ex.GetType().FullName ?? string.Empty);
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonUnknown);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Probe deliberately maps any unexpected failure to the categorical Unknown reason + structured WARNING log so a single check throwing does not poison the runner's aggregate result.")]
    public async Task<ArmProbeResult> ProbeIdentityAuthorizationAsync(NamespaceArmId armId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(armId);
        _ = Activity.Current;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_options.PerCheckTimeout);

        try
        {
            _ = await AcquireArmTokenAsync(linked.Token).ConfigureAwait(false);
            return new ArmProbeResult(
                ValidationCheckOutcome.Pass,
                ValidationFailureCategory.Ok,
                ReasonOk);
        }
        catch (CredentialUnavailableException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unauthorized,
                ReasonTokenExchangeFailed);
        }
        catch (AuthenticationFailedException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unauthorized,
                ReasonTokenExchangeFailed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Timeout,
                ReasonTimeout);
        }
        catch (Exception ex)
        {
            LogUnexpectedProbeFailure(ex, nameof(ProbeIdentityAuthorizationAsync), ex.GetType().FullName ?? string.Empty);
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonUnknown);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Probe deliberately maps any unexpected failure to the categorical Unknown reason + structured WARNING log so a single check throwing does not poison the runner's aggregate result.")]
    public async Task<ArmProbeResult> ProbeApiReachabilityAsync(NamespaceArmId armId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(armId);
        _ = Activity.Current;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_options.ApiReachabilityTimeout);

        try
        {
            var token = await AcquireArmTokenAsync(linked.Token).ConfigureAwait(false);
            var httpClient = _httpClientFactory.CreateClient(ArmManagementClientName);
            var uri = new Uri(
                $"https://{armId.NamespaceName}.servicebus.windows.net/$Resources?api-version={ServiceBusManagementApiVersion}",
                UriKind.Absolute);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);

            var correlationId = TryReadCorrelationId(response);

            // 200/401/403 all confirm "we reached the management endpoint."
            // Auth distinction is the IdentityAuthorization check's job.
            return response.StatusCode switch
            {
                HttpStatusCode.OK
                    or HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden =>
                    new ArmProbeResult(
                        ValidationCheckOutcome.Pass,
                        ValidationFailureCategory.Ok,
                        ReasonOk,
                        correlationId),
                _ =>
                    // Any other status (5xx, 404 on the management endpoint, etc.) is
                    // ambiguous — the endpoint isn't behaving as expected. Treat as Unknown
                    // rather than masking it as Pass.
                    new ArmProbeResult(
                        ValidationCheckOutcome.Fail,
                        ValidationFailureCategory.Unknown,
                        ReasonApiUnreachable,
                        correlationId),
            };
        }
        catch (CredentialUnavailableException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unauthorized,
                ReasonTokenExchangeFailed);
        }
        catch (AuthenticationFailedException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unauthorized,
                ReasonTokenExchangeFailed);
        }
        catch (HttpRequestException ex) when (IsNetworkLevelFailure(ex))
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonApiUnreachable);
        }
        catch (SocketException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonApiUnreachable);
        }
        catch (AuthenticationException)
        {
            // TLS handshake failure → network-tier unreachable. Distinct from
            // AuthenticationFailedException (which is the Azure.Identity token
            // exchange failure type).
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonApiUnreachable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Timeout,
                ReasonTimeout);
        }
        catch (Exception ex)
        {
            LogUnexpectedProbeFailure(ex, nameof(ProbeApiReachabilityAsync), ex.GetType().FullName ?? string.Empty);
            return new ArmProbeResult(
                ValidationCheckOutcome.Fail,
                ValidationFailureCategory.Unknown,
                ReasonUnknown);
        }
    }

    // -- helpers ------------------------------------------------------------

    private async Task<Response<ServiceBusNamespaceResource>> GetNamespaceAsync(
        NamespaceArmId armId,
        CancellationToken cancellationToken)
    {
        var resourceId = ResourceIdentifier.Parse(armId.CanonicalArmId);
        var resource = _armClient.GetServiceBusNamespaceResource(resourceId);
        return await resource.GetAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> AcquireArmTokenAsync(CancellationToken cancellationToken)
    {
        var credential = _credentialFactory.CreateCredential(_userAssignedClientId);
        var context = new TokenRequestContext(new[] { ArmScope });
        var accessToken = await credential.GetTokenAsync(context, cancellationToken).ConfigureAwait(false);
        return accessToken.Token;
    }

    private static ArmResourceSnapshot BuildSnapshot(ServiceBusNamespaceData data)
    {
        var resourceId = data.Id;
        var subscriptionId = resourceId?.SubscriptionId is { Length: > 0 } subId
            && Guid.TryParse(subId, out var parsedSub)
            ? parsedSub
            : Guid.Empty;
        var resourceGroup = resourceId?.ResourceGroupName ?? string.Empty;
        var region = data.Location.Name;

        return new ArmResourceSnapshot(
            Region: region,
            ResourceGroup: resourceGroup,
            SubscriptionId: subscriptionId,
            CapturedAtUtc: DateTimeOffset.UtcNow);
    }

    private static ArmProbeResult MapArmException(RequestFailedException ex, bool includeNotFoundAsFail)
    {
        var correlationId = TryReadCorrelationId(ex);
        return ex.Status switch
        {
            (int)HttpStatusCode.NotFound when includeNotFoundAsFail =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.NotFound,
                    ReasonNotFound,
                    correlationId),
            (int)HttpStatusCode.Unauthorized
                or (int)HttpStatusCode.Forbidden =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.Unauthorized,
                    ReasonAccessDenied,
                    correlationId),
            (int)HttpStatusCode.TooManyRequests =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.Throttled,
                    ReasonThrottled,
                    correlationId),
            _ =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.Unknown,
                    ReasonUnknown,
                    correlationId),
        };
    }

    private static ArmProbeResult MapHttpStatusToProbeResult(HttpStatusCode status, string? correlationId)
    {
        return status switch
        {
            HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.Unauthorized,
                    ReasonAccessDenied,
                    correlationId),
            HttpStatusCode.NotFound =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.NotFound,
                    ReasonNotFound,
                    correlationId),
            HttpStatusCode.TooManyRequests =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.Throttled,
                    ReasonThrottled,
                    correlationId),
            _ =>
                new ArmProbeResult(
                    ValidationCheckOutcome.Fail,
                    ValidationFailureCategory.Unknown,
                    ReasonUnknown,
                    correlationId),
        };
    }

    private static async Task<bool> EvaluatePermissionsResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("value", out var valueArray)
            || valueArray.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var entry in valueArray.EnumerateArray())
        {
            if (!entry.TryGetProperty("actions", out var actionsArray)
                || actionsArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var actionElement in actionsArray.EnumerateArray())
            {
                if (actionElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var action = actionElement.GetString();
                if (string.IsNullOrEmpty(action))
                {
                    continue;
                }

                foreach (var grantingAction in PermissionGrantingActions)
                {
                    if (string.Equals(action, grantingAction, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string? TryReadCorrelationId(Response response)
    {
        if (response is null)
        {
            return null;
        }

        return response.Headers.TryGetValue(CorrelationRequestIdHeader, out var value)
            ? value
            : null;
    }

    private static string? TryReadCorrelationId(HttpResponseMessage response)
    {
        if (response is null)
        {
            return null;
        }

        if (response.Headers.TryGetValues(CorrelationRequestIdHeader, out var values))
        {
            foreach (var v in values)
            {
                return v;
            }
        }

        return null;
    }

    private static string? TryReadCorrelationId(RequestFailedException ex)
    {
        var raw = ex.GetRawResponse();
        return raw is null ? null : TryReadCorrelationId(raw);
    }

    private static bool IsNetworkLevelFailure(HttpRequestException ex)
    {
        // No HTTP status was produced — the request failed before reaching the
        // server (DNS, connection refused, etc.). Treat anything without a
        // StatusCode as network-tier; anything else (e.g., 500 mapped to an
        // exception) routes through the status-based mapping path elsewhere.
        return ex.StatusCode is null;
    }

    [LoggerMessage(
        EventId = 8101,
        Level = LogLevel.Warning,
        Message = "ARM namespace probe {ProbeMethod} encountered an unexpected exception of type {ExceptionType}; check outcome reported as Unknown.")]
    private partial void LogUnexpectedProbeFailure(Exception exception, string probeMethod, string exceptionType);
}
