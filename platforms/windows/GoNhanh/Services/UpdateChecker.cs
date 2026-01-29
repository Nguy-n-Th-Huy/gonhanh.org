using System.Net.Http;
using System.Text.Json;
using GoNhanh.Core;

namespace GoNhanh.Services;

/// <summary>
/// Checks for updates from GitHub Releases API.
/// No external dependencies - uses System.Net.Http and System.Text.Json.
/// </summary>
public class UpdateChecker
{
    private const string GitHubApiUrl = "https://api.github.com/repos/khaphanspace/gonhanh.org/releases";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static UpdateChecker()
    {
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "GoNhanh-Windows");
    }

    /// <summary>
    /// Check GitHub Releases for available updates
    /// </summary>
    public async Task<(UpdateCheckResult Result, UpdateInfo? Info, string? Error)> CheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(GitHubApiUrl);

            // Handle rate limiting
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return (UpdateCheckResult.Error, null, "GitHub API rate limit exceeded. Try again later.");
            }

            if (!response.IsSuccessStatusCode)
                return (UpdateCheckResult.Error, null, $"Server error: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            return ParseResponse(json);
        }
        catch (TaskCanceledException)
        {
            return (UpdateCheckResult.Error, null, "Request timeout");
        }
        catch (HttpRequestException ex)
        {
            return (UpdateCheckResult.Error, null, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateChecker: {ex.Message}");
            return (UpdateCheckResult.Error, null, ex.Message);
        }
    }

    private (UpdateCheckResult, UpdateInfo?, string?) ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var releases = doc.RootElement;

            if (releases.ValueKind != JsonValueKind.Array)
                return (UpdateCheckResult.Error, null, "Invalid response format");

            var currentVersion = AppMetadata.Version;
            string? bestVersion = null;
            JsonElement? bestRelease = null;

            foreach (var release in releases.EnumerateArray())
            {
                // Skip drafts and prereleases
                if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean())
                    continue;
                if (release.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean())
                    continue;

                if (!release.TryGetProperty("tag_name", out var tagName))
                    continue;

                var version = tagName.GetString()?.TrimStart('v') ?? "";
                if (string.IsNullOrEmpty(version))
                    continue;

                // Find the highest version
                if (bestVersion == null || CompareVersions(version, bestVersion) > 0)
                {
                    bestVersion = version;
                    bestRelease = release;
                }
            }

            if (bestVersion == null || bestRelease == null)
                return (UpdateCheckResult.UpToDate, null, null);

            // Check if update available (best version > current version)
            if (CompareVersions(bestVersion, currentVersion) <= 0)
                return (UpdateCheckResult.UpToDate, null, null);

            // Find Windows download asset (.exe or .zip with "windows" in name)
            string? downloadUrl = null;
            long fileSize = 0;

            if (bestRelease.Value.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameElement))
                        continue;
                    var name = nameElement.GetString()?.ToLowerInvariant() ?? "";
                    if (name.Contains("windows") && (name.EndsWith(".exe") || name.EndsWith(".zip")))
                    {
                        if (asset.TryGetProperty("browser_download_url", out var urlElement))
                            downloadUrl = urlElement.GetString();
                        if (asset.TryGetProperty("size", out var sizeElement))
                            fileSize = sizeElement.GetInt64();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
                return (UpdateCheckResult.Error, null, "No Windows release found");

            // Parse release metadata
            var releaseNotes = bestRelease.Value.TryGetProperty("body", out var body)
                ? body.GetString() ?? "" : "";

            DateTime? publishedAt = null;
            if (bestRelease.Value.TryGetProperty("published_at", out var published))
            {
                if (DateTime.TryParse(published.GetString(), out var dt))
                    publishedAt = dt;
            }

            var info = new UpdateInfo(bestVersion, downloadUrl, releaseNotes, publishedAt, fileSize);
            return (UpdateCheckResult.Available, info, null);
        }
        catch (JsonException ex)
        {
            return (UpdateCheckResult.Error, null, $"Failed to parse response: {ex.Message}");
        }
    }

    /// <summary>
    /// Compare semantic versions: returns -1 (v1 &lt; v2), 0 (equal), or 1 (v1 &gt; v2)
    /// </summary>
    public static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

        var maxLen = Math.Max(parts1.Length, parts2.Length);
        for (int i = 0; i < maxLen; i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;
            if (p1 < p2) return -1;
            if (p1 > p2) return 1;
        }
        return 0;
    }
}
