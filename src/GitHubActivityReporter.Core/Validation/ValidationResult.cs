namespace GitHubActivityReporter.Core.Validation;

public enum ValidationSeverity
{
    Warning,
    Error
}

/// <summary>A single validation finding. Messages are always safe to print.</summary>
public sealed record ValidationIssue
{
    public required string RuleId { get; init; }

    public required ValidationSeverity Severity { get; init; }

    /// <summary>Safe, masked description of the finding.</summary>
    public required string Message { get; init; }

    /// <summary>Artifact the issue was found in, if any.</summary>
    public string? Target { get; init; }

    public override string ToString()
        => Target is null
            ? $"[{Severity}] {RuleId}: {Message}"
            : $"[{Severity}] {RuleId}: {Message} ({Target})";
}

public sealed record ValidationResult
{
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

    public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Errors => Issues.Where(i => i.Severity == ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Warnings => Issues.Where(i => i.Severity == ValidationSeverity.Warning);

    public static ValidationResult Success() => new();

    public static ValidationResult FromIssues(IEnumerable<ValidationIssue> issues)
        => new() { Issues = issues.ToArray() };

    public ValidationResult Merge(ValidationResult other)
        => new() { Issues = Issues.Concat(other.Issues).ToArray() };
}
