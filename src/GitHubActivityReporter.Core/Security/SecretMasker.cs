using System.Text;

namespace GitHubActivityReporter.Core.Security;

/// <summary>Masks sensitive values so they can be mentioned in diagnostics safely.</summary>
public static class SecretMasker
{
    public const string FullMask = "***";

    /// <summary>Masks a value keeping only a very small, non reversible hint.</summary>
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return FullMask;
        }

        if (value.Length <= 6)
        {
            return FullMask;
        }

        return string.Concat(value.AsSpan(0, 2), FullMask, value.AsSpan(value.Length - 2, 2));
    }

    /// <summary>Replaces every occurrence of the given secrets inside <paramref name="text"/>.</summary>
    public static string MaskAll(string? text, IEnumerable<string?> secrets)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        ArgumentNullException.ThrowIfNull(secrets);

        var builder = new StringBuilder(text);
        foreach (var secret in secrets)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 4)
            {
                continue;
            }

            builder.Replace(secret, FullMask);
        }

        return builder.ToString();
    }
}
