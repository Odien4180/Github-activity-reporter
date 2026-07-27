using System.Security.Cryptography;
using System.Text;

namespace GitHubActivityReporter.Core.Security;

/// <summary>
/// Creates non reversible identifiers for private repositories.
/// The salt is generated per process so identifiers cannot be correlated across runs.
/// </summary>
public static class OpaqueIdentifier
{
    private static readonly string ProcessSalt = Guid.NewGuid().ToString("N");

    public static string Create(string value) => Create(value, ProcessSalt);

    public static string Create(string value, string salt)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(salt);

        var bytes = Encoding.UTF8.GetBytes(salt + "|" + value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
