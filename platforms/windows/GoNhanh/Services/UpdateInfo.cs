namespace GoNhanh.Services;

/// <summary>
/// Information about an available update from GitHub Releases
/// </summary>
public record UpdateInfo(
    string Version,
    string DownloadUrl,
    string ReleaseNotes,
    DateTime? PublishedAt,
    long FileSize
);

/// <summary>
/// Current state of the update process
/// </summary>
public enum UpdateState
{
    /// <summary>No update activity</summary>
    Idle,
    /// <summary>Checking for updates</summary>
    Checking,
    /// <summary>Update available</summary>
    Available,
    /// <summary>Already on latest version</summary>
    UpToDate,
    /// <summary>Downloading update</summary>
    Downloading,
    /// <summary>Download completed</summary>
    Downloaded,
    /// <summary>Error occurred</summary>
    Error
}

/// <summary>
/// Result of checking for updates
/// </summary>
public enum UpdateCheckResult
{
    /// <summary>New version available</summary>
    Available,
    /// <summary>Already on latest version</summary>
    UpToDate,
    /// <summary>Error checking for updates</summary>
    Error
}
