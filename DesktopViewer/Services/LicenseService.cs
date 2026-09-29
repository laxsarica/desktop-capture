using System.Security.Cryptography;
using System.Text;

namespace DesktopViewer.Services;

/// <summary>
/// Validates license keys on application startup.
/// Keys are verified against a set of pre-approved SHA256 hashes.
/// </summary>
public static class LicenseService
{
    // Pre-computed SHA256 hashes of valid license keys
    private static readonly HashSet<string> ValidKeyHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CA4C3E8F7D27610FB1CDFBBEF27CA1A01A3E42BC267EACCC82263F5F26619A68",
        "A94B519246097F8FF578F9C1F8014FBFD82ED453C4C8269FB34201457AE99678",
        "0F288AA2BBD29E4D9F932CFF1DA7ECF68C11923242BA376C90249BB27F0C5BAB",
    };

    /// <summary>
    /// Validates the provided license key.
    /// </summary>
    public static bool IsValid(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        var hash = ComputeHash(licenseKey.Trim());
        return ValidKeyHashes.Contains(hash);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
