namespace GitHubActivityReporter.Core.Security;

/// <summary>
/// In-memory registry of strings that originate from private repositories
/// (repository names, owners, organisations, urls, branch names, ...).
/// The registry exists solely so the <see cref="Validation.PrivacyValidator"/> can
/// prove that none of those strings leaked into a rendered artifact.
/// It must never be serialized, logged or persisted.
/// </summary>
public interface IPrivateTermRegistry
{
    void Add(string? term);

    void AddRange(IEnumerable<string?> terms);

    IReadOnlyCollection<string> Terms { get; }

    int Count { get; }

    void Clear();
}

public sealed class InMemoryPrivateTermRegistry : IPrivateTermRegistry
{
    private readonly HashSet<string> _terms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public void Add(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return;
        }

        var trimmed = term.Trim();
        if (trimmed.Length < 3)
        {
            return;
        }

        lock (_gate)
        {
            _terms.Add(trimmed);
        }
    }

    public void AddRange(IEnumerable<string?> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        foreach (var term in terms)
        {
            Add(term);
        }
    }

    public IReadOnlyCollection<string> Terms
    {
        get
        {
            lock (_gate)
            {
                return _terms.ToArray();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _terms.Count;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _terms.Clear();
        }
    }

    /// <summary>Never expose the registered terms through diagnostics.</summary>
    public override string ToString() => $"{nameof(InMemoryPrivateTermRegistry)}(terms: {Count})";
}
