using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace LT1Diagnostics.App.Views;

public sealed partial class MainWindow : Window
{
    private const string WindowsDownloadUrl =
        "https://github.com/jmaietta/4L60-Diagnostics/releases/latest/download/4L60-Diagnostics-win-x64.zip";
    private const string LinuxDownloadUrl =
        "https://github.com/jmaietta/4L60-Diagnostics/releases/latest/download/4L60-Diagnostics-linux-x64.tar.gz";

    public MainWindow() => InitializeComponent();

    public async Task<string?> PickRawSessionAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open a Maietta Diagnostics session",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Maietta Diagnostics raw session")
                    {
                        Patterns = ["*.lt1raw"],
                    },
                ],
            });
        return files.Count == 1 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> PickSavePathAsync(string suggestedName, string extension)
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = extension == "html" ? "Save diagnostic report" : "Export measurements",
                SuggestedFileName = suggestedName,
                DefaultExtension = extension,
                FileTypeChoices =
                [
                    extension == "html"
                        ? new FilePickerFileType("Web report") { Patterns = ["*.html"] }
                        : new FilePickerFileType("Comma-separated values") { Patterns = ["*.csv"] },
                ],
            });
        return file?.Path.LocalPath;
    }

    private async void CopyWindowsDownloadLink_Click(object? sender, RoutedEventArgs e) =>
        await CopyDownloadLinkAsync(sender, WindowsDownloadUrl);

    private async void CopyLinuxDownloadLink_Click(object? sender, RoutedEventArgs e) =>
        await CopyDownloadLinkAsync(sender, LinuxDownloadUrl);

    private async Task CopyDownloadLinkAsync(object? sender, string address)
    {
        if (sender is not Button button)
        {
            return;
        }

        try
        {
            IClipboard? clipboard = Clipboard;
            if (clipboard is null)
            {
                button.Content = "Could not copy — select the link below";
                return;
            }

            await clipboard.SetTextAsync(address);
            button.Content = "Link copied";
        }
        catch
        {
            button.Content = "Could not copy — select the link below";
        }
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }
}
