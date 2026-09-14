using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace SimsConverter.App.Services;

public class AvaloniaFilePickerService : IFilePickerService
{
    private readonly Func<Window?> _windowProvider;

    public AvaloniaFilePickerService(Func<Window?> windowProvider)
    {
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
    }

    public async Task<string?> OpenPackageFilePickerAsync()
    {
        var window = _windowProvider();
        if (window == null)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel?.StorageProvider == null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Sims Package or Sims3Pack File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Sims Containers (*.package; *.sims3pack)")
                {
                    Patterns = new[] { "*.package", "*.sims3pack" }
                },
                new FilePickerFileType("DBPF Package (*.package)")
                {
                    Patterns = new[] { "*.package" }
                },
                new FilePickerFileType("Sims3Pack Container (*.sims3pack)")
                {
                    Patterns = new[] { "*.sims3pack" }
                },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            return files[0].TryGetLocalPath();
        }

        return null;
    }

    public async Task<string?> OpenFolderPickerAsync(string? title = null)
    {
        var window = _windowProvider();
        if (window == null)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel?.StorageProvider == null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Select Export Destination Folder" : title,
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            return folders[0].TryGetLocalPath();
        }

        return null;
    }

    public async Task<string?> SavePackageFilePickerAsync()
    {
        var window = _windowProvider();
        if (window == null)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel?.StorageProvider == null)
        {
            return null;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Select TS4 Target Package Location",
            DefaultExtension = "package",
            SuggestedFileName = "converted_ts4.package",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("TS4 Package (*.package)")
                {
                    Patterns = new[] { "*.package" }
                },
                FilePickerFileTypes.All
            }
        });

        if (file != null)
        {
            return file.TryGetLocalPath();
        }

        return null;
    }

    public async Task CopyToClipboardAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var window = _windowProvider();
        if (window == null) return;

        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(text).ConfigureAwait(false);
        }
    }

    public Task OpenFileWithDefaultAppAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
        {
            return Task.CompletedTask;
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch { }

        return Task.CompletedTask;
    }
}
