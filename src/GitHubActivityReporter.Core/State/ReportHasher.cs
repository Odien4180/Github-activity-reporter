using System.Security.Cryptography;
using System.Text;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.State;

/// <summary>Computes a stable hash of rendered output used for change detection.</summary>
public static class ReportHasher
{
    public static string Hash(IEnumerable<RenderedReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        var builder = new StringBuilder();
        foreach (var artifact in reports
                     .SelectMany(r => r.Artifacts)
                     .OrderBy(a => a.RelativePath, StringComparer.Ordinal))
        {
            builder.Append(artifact.RelativePath).Append('\n').Append(artifact.Content).Append('\n');
        }

        return Hash(builder.ToString());
    }

    public static string Hash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return "sha256-" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
