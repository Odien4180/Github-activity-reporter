namespace GitHubActivityReporter.Core.Configuration;

/// <summary>Renderer and publisher identifiers implemented by this version.</summary>
public static class KnownRenderers
{
    public const string CompactMarkdown = "compact-markdown";
    public const string NormalizedJson = "normalized-json";

    // Not implemented yet (Phase 2 / Phase 3). Enabling them is rejected by the
    // configuration validator so a user never gets a silently missing output.
    public const string SvgDashboard = "svg-dashboard";
    public const string StaticHtml = "static-html";
    public const string EmailHtml = "email-html";
    public const string SlackBlocks = "slack-blocks";

    public static IReadOnlySet<string> Implemented { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CompactMarkdown,
        NormalizedJson
    };

    public static IReadOnlySet<string> Planned { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SvgDashboard,
        StaticHtml,
        EmailHtml,
        SlackBlocks
    };
}

public static class KnownPublishers
{
    public const string Local = "local";
    public const string GitHubProfile = "github-profile";

    // Planned publishers, see IMPLEMENTATION_PLAN.md.
    public const string GitHubPages = "github-pages";
    public const string Email = "email";
    public const string Slack = "slack";

    public static IReadOnlySet<string> Implemented { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Local,
        GitHubProfile
    };
}
