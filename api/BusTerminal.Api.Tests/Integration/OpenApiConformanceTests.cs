using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BusTerminal.Api.Tests.Integration;

/// <summary>
/// T094 / Phase 9 polish — contract conformance for the slice-003 OpenAPI surface.
///
/// Reads <c>contracts/role-probes.openapi.yaml</c> and <c>contracts/whoami.openapi.yaml</c>
/// from the active spec, extracts each (path, HTTP method) pair, and asserts the
/// **live** OpenAPI document emitted by the API (at <c>/openapi/v1.json</c>) declares
/// every one of them. Drift between the contracts and the live shape is a defect.
///
/// Scope is deliberately limited to path/method existence — a minimum-viable
/// drift detector. Deeper schema-level conformance (request bodies, response
/// schemas) is out of scope for this slice and tracked under the
/// role-permission-matrix contract instead.
/// </summary>
public sealed class OpenApiConformanceTests : IClassFixture<OpenApiAppFactory>
{
    private static readonly Regex PathLineRegex =
        new(@"^  (/\S+?):\s*$", RegexOptions.Compiled);

    private static readonly Regex MethodLineRegex =
        new(@"^    (get|post|put|delete|patch|head|options):\s*$", RegexOptions.Compiled);

    private readonly OpenApiAppFactory _factory;

    public OpenApiConformanceTests(OpenApiAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LiveOpenApi_DeclaresEveryPathAndMethodFromContracts()
    {
        var contractOperations = LoadContractOperations();
        contractOperations.Should().NotBeEmpty(
            "the test would silently pass if no contract operations were loaded");

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var liveOperations = ExtractOperationsFromOpenApi(document);

        foreach (var op in contractOperations)
        {
            liveOperations.Should().Contain(op,
                $"contract declares {op.Method.ToUpperInvariant()} {op.Path}; live OpenAPI must too");
        }
    }

    private static HashSet<Operation> LoadContractOperations()
    {
        var contractsDir = FindContractsDirectory();
        var ops = new HashSet<Operation>();

        foreach (var yaml in Directory.EnumerateFiles(contractsDir, "*.openapi.yaml"))
        {
            ops.UnionWith(ExtractOperationsFromYaml(File.ReadAllLines(yaml)));
        }

        return ops;
    }

    private static IEnumerable<Operation> ExtractOperationsFromYaml(string[] lines)
    {
        string? currentPath = null;
        var inPathsBlock = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("paths:", StringComparison.Ordinal))
            {
                inPathsBlock = true;
                continue;
            }
            if (!inPathsBlock)
            {
                continue;
            }
            // Leaving the paths block: the next top-level key terminates it.
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                inPathsBlock = false;
                currentPath = null;
                continue;
            }

            var pathMatch = PathLineRegex.Match(line);
            if (pathMatch.Success)
            {
                currentPath = pathMatch.Groups[1].Value;
                continue;
            }

            if (currentPath is null)
            {
                continue;
            }

            var methodMatch = MethodLineRegex.Match(line);
            if (methodMatch.Success)
            {
                yield return new Operation(currentPath, methodMatch.Groups[1].Value);
            }
        }
    }

    private static HashSet<Operation> ExtractOperationsFromOpenApi(JsonDocument document)
    {
        var ops = new HashSet<Operation>();
        if (!document.RootElement.TryGetProperty("paths", out var paths))
        {
            return ops;
        }
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                ops.Add(new Operation(path.Name, method.Name.ToLowerInvariant()));
            }
        }
        return ops;
    }

    private static string FindContractsDirectory()
    {
        // Walk up from the test bin/ until we hit the repo root (contains
        // `specs/003-auth-and-identity/contracts`).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "specs", "003-auth-and-identity", "contracts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate specs/003-auth-and-identity/contracts walking up from "
            + AppContext.BaseDirectory);
    }

    private readonly record struct Operation(string Path, string Method);
}

public sealed class OpenApiAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);
        builder.UseSetting("AzureAd:TenantId", "development");
        builder.UseSetting("AzureAd:Instance", "https://login.microsoftonline.com/");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("AzureAd:Audience", "api://busterminal-dev");
    }
}
