using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LT1Diagnostics.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public async Task<string?> PickRawSessionAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open a 4L60 Diagnostics session",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("4L60 Diagnostics raw session")
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

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }
}
