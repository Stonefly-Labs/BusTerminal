using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Domain.Validation;

// Spec 004 / Q3 + FR-013. Dispatches over registered rules. Rules that opt out of a
// resource via AppliesTo() are skipped.
public sealed partial class ValidationEngine
{
    private readonly IEnumerable<IValidationRule> _rules;
    private readonly IServiceProvider _services;
    private readonly ILogger<ValidationEngine> _logger;
    private readonly TimeProvider _time;

    public ValidationEngine(
        IEnumerable<IValidationRule> rules,
        IServiceProvider services,
        ILogger<ValidationEngine> logger,
        TimeProvider time)
    {
        _rules = rules;
        _services = services;
        _logger = logger;
        _time = time;
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "Validation rule {Rule} threw while validating resource {ResourceId}; recording as Error finding.")]
    private partial void LogRuleFailure(Exception exception, string rule, ResourceId resourceId);

    public Task<ValidationResult> ValidateAsync(
        Resource resource,
        Func<ResourceId, Resource?> relationshipResolver,
        Func<Resource, bool> duplicateDetector,
        LifecycleState? previousLifecycle = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        cancellationToken.ThrowIfCancellationRequested();

        var context = new ValidationContext
        {
            RelationshipResolver = relationshipResolver,
            DuplicateDetector = duplicateDetector,
            Services = _services,
            PreviousLifecycle = previousLifecycle,
        };

        var findings = new List<ValidationFinding>();
        foreach (var rule in _rules)
        {
            if (!rule.AppliesTo(resource.GetType()))
            {
                continue;
            }

            findings.AddRange(RunRule(rule, resource, context));
        }

        return Task.FromResult(new ValidationResult(_time.GetUtcNow(), findings));
    }

    // Catch-all on rule execution is intentional: a faulty third-party-registered
    // rule must not poison the entire validation pass. The failure becomes a
    // structured Error finding (so it's persistable and visible to operators) and
    // is logged for triage.
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Engine deliberately isolates rule failures into structured findings.")]
    private IEnumerable<ValidationFinding> RunRule(
        IValidationRule rule,
        Resource resource,
        ValidationContext context)
    {
        try
        {
            return [.. rule.Validate(resource, context)];
        }
        catch (Exception ex)
        {
            LogRuleFailure(ex, rule.GetType().Name, resource.Id);

            return
            [
                new ValidationFinding(
                    RuleId: $"engine.ruleFailure.{rule.GetType().Name}",
                    Severity: ValidationSeverity.Error,
                    Message: $"Validation rule {rule.GetType().Name} threw: {ex.Message}",
                    EvaluatedAt: _time.GetUtcNow()),
            ];
        }
    }
}
