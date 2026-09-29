using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopViewer.Services;

/// <summary>
/// Validates license keys on application startup via Cloudflare Worker API.
/// </summary>
public static class LicenseService
{
    private static readonly HttpClient _http = new();
    private const string ValidationUrl = "https://license-api.vivek-jainit.workers.dev/api/validate";

    /// <summary>
    /// Validates the provided license key.
    /// </summary>
    public static async Task<bool> IsValidAsync(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        try
        {
            var response = await _http.PostAsJsonAsync(ValidationUrl, new { key = licenseKey.Trim() });
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<ValidationResult>();
            return result?.Valid == true;
        }
        catch
        {
            // If the API is down or any error occurs, consider the key invalid to be safe.
            return false;
        }
    }
}

public class ValidationResult
{
    public bool Valid { get; set; }
    public string? Message { get; set; }
}
