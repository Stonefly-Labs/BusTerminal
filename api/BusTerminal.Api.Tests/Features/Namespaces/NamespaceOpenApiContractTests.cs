using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces;

// Spec 008 / T143 + T154. Two assertions over the runtime OpenAPI document:
//
//   1. T143 — every (path, method) declared by
//      `specs/008-namespace-onboarding/contracts/namespace-onboarding-api.yaml`
//      MUST be present in the runtime `/openapi/v1.json` under the documented
//      server prefix `/api`. Drift between the authoring source and the runtime
//      document is a defect (mirrors the spec-003 OpenApiConformanceTests
//      pattern + the spec-006 OpenApiContractTests path-existence smoke).
//
//   2. T154 — FR-026 regression: the runtime OpenAPI document MUST NOT declare
//      a DELETE operation on ANY `/api/namespaces/*` path. Onboarded namespaces
//      are lifecycle-managed (Archive) — never physically deleted.
public sealed class NamespaceOpenApiContractTests : IClassFixture<NamespacesContractFactory>
{
    private const string ServerPrefix = "/api";

    private static readonly Regex PathLineRegex =
        new(@"^  (/\S+?):\s*$", RegexOptions.Compiled);

    private static readonly Regex MethodLineRegex =
        new(@"^    (get|post|put|delete|patch|head|options):\s*$", RegexOptions.Compiled);

    private readonly NamespacesContractFactory _factory;

    public NamespaceOpenApiContractTests(NamespacesContractFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LiveOpenApi_DeclaresEveryContractOperation()
    {
        var contractOperations = LoadContractOperations();
        contractOperations.Should().NotBeEmpty(
            "the test would silently pass if no contract operations were loaded");

        var liveOperations = await LoadLiveOperationsAsync();

        foreach (var (path, method) in contractOperations)
        {
            var expected = (ServerPrefix + path, method);
            liveOperations.Should().Contain(expected,
                $"contract declares {method.ToUpperInvariant()} {path}; live OpenAPI must too (with /api prefix)");
        }
    }

    [Fact]
    public async Task LiveOpenApi_NoDeleteOnAnyNamespaceRoute()
    {
        var liveOperations = await LoadLiveOperationsAsync();
        var offending = liveOperations
            .Where(op => op.Path.StartsWith("/api/namespaces", StringComparison.Ordinal)
                          && string.Equals(op.Method, "delete", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        offending.Should().BeEmpty(
            "FR-026: spec-008 namespaces are lifecycle-managed (Archive), never physically deleted — runtime OpenAPI document must declare no DELETE operations under /api/namespaces.");
    }

    private async Task<HashSet<(string Path, string Method)>> LoadLiveOperationsAsync()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var ops = new HashSet<(string, string)>();
        if (!document.RootElement.TryGetProperty("paths", out var paths))
        {
            return ops;
        }

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                ops.Add((path.Name, method.Name.ToLowerInvariant()));
            }
        }
        return ops;
    }

    private static HashSet<(string Path, string Method)> LoadContractOperations()
    {
        var contractPath = FindContractFile();
        var lines = File.ReadAllLines(contractPath);

        var ops = new HashSet<(string, string)>();
        string? currentPath = null;
        var inPathsBlock = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("paths:", StringComparison.Ordinal))
            {
                inPathsBlock = true;
                continue;
            }
            if (!inPathsBlock) continue;
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

            if (currentPath is null) continue;

            var methodMatch = MethodLineRegex.Match(line);
            if (methodMatch.Success)
            {
                ops.Add((currentPath, methodMatch.Groups[1].Value));
            }
        }

        return ops;
    }

    private static string FindContractFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "specs",
                "008-namespace-onboarding",
                "contracts",
                "namespace-onboarding-api.yaml");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "Could not locate specs/008-namespace-onboarding/contracts/namespace-onboarding-api.yaml walking up from "
            + AppContext.BaseDirectory);
    }
}
