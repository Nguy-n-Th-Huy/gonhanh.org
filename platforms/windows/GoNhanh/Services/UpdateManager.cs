using System.IO;
using System.Net.Http;
using Microsoft.Win32;

namespace GoNhanh.Services;

/// <summary>
/// Manages update checking, downloading, and state.
/// Supports silent background checks with 24h interval.
/// Download only - user installs manually.
/// </summary>
public class UpdateManager
{
    private const string RegistryPath = @"SOFTWARE\GoNhanh";
    private const string LastCheckKey = "UpdateLastCheck";
    private const string SkipVersionKey = "UpdateSkipVersion";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly UpdateChecker _checker = new();
    private static readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _downloadCts;

    #region Properties

    /// <summary>Current state of the update process</summary>
    public UpdateState State { get; private set; } = UpdateState.Idle;

    /// <summary>Information about available update (when State == Available)</summary>
    public UpdateInfo? CurrentUpdate { get; private set; }

    /// <summary>Download progress (0.0 to 1.0)</summary>
    public double DownloadProgress { get; private set; }

    /// <summary>Error message (when State == Error)</summary>
    public string? ErrorMessage { get; private set; }

    #endregion

    #region Events

    /// <summary>Fired when State, DownloadProgress, or ErrorMessage changes</summary>
    public event Action? StateChanged;

    #endregion

    #region Public Methods

    /// <summary>
    /// Check for updates from GitHub Releases
    /// </summary>
    /// <param name="silent">If true, respects 24h interval and skipped versions</param>
    public async Task CheckForUpdatesAsync(bool silent = false)
    {
        // For silent checks, respect the 24h interval
        if (silent && !ShouldCheck())
        {
            return;
        }

        if (!silent)
            SetState(UpdateState.Checking);

        var (result, info, error) = await _checker.CheckAsync();
        SaveLastCheckTime();

        switch (result)
        {
            case UpdateCheckResult.Available when info != null:
                // Skip if user chose to skip this version (silent mode only)
                if (silent && GetSkippedVersion() == info.Version)
                {
                    SetState(UpdateState.Idle);
                    return;
                }
                CurrentUpdate = info;
                SetState(UpdateState.Available);
                break;

            case UpdateCheckResult.UpToDate:
                SetState(UpdateState.UpToDate);
                break;

            case UpdateCheckResult.Error:
                ErrorMessage = error;
                if (!silent) // Don't show errors for silent checks
                    SetState(UpdateState.Error);
                else
                    SetState(UpdateState.Idle);
                break;
        }
    }

    /// <summary>
    /// Download the update to specified folder
    /// </summary>
    public async Task DownloadUpdateAsync(string destinationFolder)
    {
        if (CurrentUpdate == null) return;

        SetState(UpdateState.Downloading);
        DownloadProgress = 0;
        _downloadCts = new CancellationTokenSource();

        try
        {
            // Ensure destination folder exists
            Directory.CreateDirectory(destinationFolder);

            var fileName = Path.GetFileName(new Uri(CurrentUpdate.DownloadUrl).LocalPath);
            var filePath = Path.Combine(destinationFolder, fileName);

            using var response = await _httpClient.GetAsync(
                CurrentUpdate.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                _downloadCts.Token);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? CurrentUpdate.FileSize;
            var buffer = new byte[8192];
            var bytesRead = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(_downloadCts.Token);
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            int read;
            while ((read = await contentStream.ReadAsync(buffer, _downloadCts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), _downloadCts.Token);
                bytesRead += read;
                DownloadProgress = totalBytes > 0 ? (double)bytesRead / totalBytes : 0;
                StateChanged?.Invoke();
            }

            SetState(UpdateState.Downloaded);

            // Open folder with file selected in Explorer
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (OperationCanceledException)
        {
            SetState(UpdateState.Idle);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SetState(UpdateState.Error);
            System.Diagnostics.Debug.WriteLine($"UpdateManager: Download failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel ongoing download
    /// </summary>
    public void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    /// <summary>
    /// Skip the current update version (won't prompt again for this version)
    /// </summary>
    public void SkipVersion()
    {
        if (CurrentUpdate != null)
            SaveSkippedVersion(CurrentUpdate.Version);
        CurrentUpdate = null;
        SetState(UpdateState.Idle);
    }

    /// <summary>
    /// Reset state to idle
    /// </summary>
    public void Reset()
    {
        CurrentUpdate = null;
        ErrorMessage = null;
        DownloadProgress = 0;
        SetState(UpdateState.Idle);
    }

    #endregion

    #region Private Methods

    private void SetState(UpdateState state)
    {
        State = state;
        StateChanged?.Invoke();
    }

    private bool ShouldCheck()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var lastCheck = key?.GetValue(LastCheckKey);
            if (lastCheck == null) return true;

            var lastCheckTime = DateTime.FromBinary((long)lastCheck);
            return DateTime.Now - lastCheckTime >= CheckInterval;
        }
        catch
        {
            return true;
        }
    }

    private void SaveLastCheckTime()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(LastCheckKey, DateTime.Now.ToBinary(), RegistryValueKind.QWord);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateManager: Failed to save last check time: {ex.Message}");
        }
    }

    private string? GetSkippedVersion()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue(SkipVersionKey) as string;
        }
        catch
        {
            return null;
        }
    }

    private void SaveSkippedVersion(string version)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(SkipVersionKey, version, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateManager: Failed to save skipped version: {ex.Message}");
        }
    }

    #endregion
}
