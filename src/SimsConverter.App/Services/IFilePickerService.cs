using System.Threading.Tasks;

namespace SimsConverter.App.Services;

public interface IFilePickerService
{
    Task<string?> OpenPackageFilePickerAsync();
    Task<string?> OpenFolderPickerAsync(string? title = null);
    Task<string?> SavePackageFilePickerAsync();
    Task CopyToClipboardAsync(string text);
    Task OpenFileWithDefaultAppAsync(string filePath);
}
