using System.Threading.Tasks;

namespace SimsConverter.App.Services;

public interface IFilePickerService
{
    Task<string?> OpenPackageFilePickerAsync();
    Task<string?> OpenFolderPickerAsync();
    Task<string?> SavePackageFilePickerAsync();
}
