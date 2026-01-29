using System.Windows;
using GoNhanh.Core;
using GoNhanh.Services;

namespace GoNhanh.Views;

/// <summary>
/// Update window showing update status, release notes, and download progress.
/// Binds to UpdateManager for state management.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateManager _updateManager = new();

    public UpdateWindow()
    {
        InitializeComponent();
        _updateManager.StateChanged += OnStateChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _updateManager.CheckForUpdatesAsync(silent: false);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _updateManager.StateChanged -= OnStateChanged;
        _updateManager.CancelDownload();
    }

    private void OnStateChanged()
    {
        // Marshal to UI thread
        Dispatcher.Invoke(UpdateUI);
    }

    private void UpdateUI()
    {
        switch (_updateManager.State)
        {
            case UpdateState.Checking:
                ShowChecking();
                break;

            case UpdateState.Available:
                ShowAvailable();
                break;

            case UpdateState.UpToDate:
                ShowUpToDate();
                break;

            case UpdateState.Downloading:
                ShowDownloading();
                break;

            case UpdateState.Downloaded:
                ShowDownloaded();
                break;

            case UpdateState.Error:
                ShowError();
                break;

            case UpdateState.Idle:
            default:
                // Window likely closing or reset
                break;
        }
    }

    private void ShowChecking()
    {
        TitleText.Text = "Đang kiểm tra cập nhật...";
        VersionText.Text = $"Phiên bản hiện tại: {AppMetadata.Version}";

        NotesPanel.Visibility = Visibility.Collapsed;
        StatusPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;
        SkipButton.Visibility = Visibility.Collapsed;

        ActionButton.Content = "Đóng";
        ActionButton.IsEnabled = true;
    }

    private void ShowAvailable()
    {
        var update = _updateManager.CurrentUpdate!;

        TitleText.Text = $"Có phiên bản mới: {update.Version}";
        VersionText.Text = $"Phiên bản hiện tại: {AppMetadata.Version}";

        // Show release notes
        if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
        {
            NotesText.Text = update.ReleaseNotes;
            NotesPanel.Visibility = Visibility.Visible;
        }
        else
        {
            NotesPanel.Visibility = Visibility.Collapsed;
        }

        StatusPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;

        SkipButton.Visibility = Visibility.Visible;
        ActionButton.Content = "Tải về";
        ActionButton.IsEnabled = true;
    }

    private void ShowUpToDate()
    {
        TitleText.Text = "Bạn đang dùng phiên bản mới nhất";
        VersionText.Text = $"Phiên bản: {AppMetadata.Version}";

        NotesPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;

        StatusIcon.Text = "✓";
        StatusText.Text = "Không có bản cập nhật mới.";
        StatusPanel.Visibility = Visibility.Visible;

        SkipButton.Visibility = Visibility.Collapsed;
        ActionButton.Content = "Đóng";
        ActionButton.IsEnabled = true;
    }

    private void ShowDownloading()
    {
        var update = _updateManager.CurrentUpdate!;
        var percent = (int)(_updateManager.DownloadProgress * 100);

        TitleText.Text = $"Đang tải {update.Version}...";

        ProgressBar.Value = percent;
        ProgressText.Text = $"{percent}%";

        NotesPanel.Visibility = Visibility.Collapsed;
        StatusPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;

        SkipButton.Visibility = Visibility.Collapsed;
        ActionButton.Content = "Hủy";
        ActionButton.IsEnabled = true;
    }

    private void ShowDownloaded()
    {
        TitleText.Text = "Tải về thành công!";
        VersionText.Text = "File đã được mở trong Explorer.";

        NotesPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;

        StatusIcon.Text = "✓";
        StatusText.Text = "Vui lòng đóng ứng dụng và chạy file cài đặt.";
        StatusPanel.Visibility = Visibility.Visible;

        SkipButton.Visibility = Visibility.Collapsed;
        ActionButton.Content = "Đóng";
        ActionButton.IsEnabled = true;
    }

    private void ShowError()
    {
        TitleText.Text = "Không thể kiểm tra cập nhật";
        VersionText.Text = "";

        NotesPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;

        StatusIcon.Text = "✕";
        StatusText.Text = _updateManager.ErrorMessage ?? "Đã xảy ra lỗi. Vui lòng thử lại sau.";
        StatusPanel.Visibility = Visibility.Visible;

        SkipButton.Visibility = Visibility.Collapsed;
        ActionButton.Content = "Đóng";
        ActionButton.IsEnabled = true;
    }

    private async void OnAction(object sender, RoutedEventArgs e)
    {
        switch (_updateManager.State)
        {
            case UpdateState.Available:
                // Download to user's Downloads folder
                var downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                downloadsPath = System.IO.Path.Combine(downloadsPath, "Downloads");
                await _updateManager.DownloadUpdateAsync(downloadsPath);
                break;

            case UpdateState.Downloading:
                _updateManager.CancelDownload();
                break;

            default:
                Close();
                break;
        }
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _updateManager.SkipVersion();
        Close();
    }
}
