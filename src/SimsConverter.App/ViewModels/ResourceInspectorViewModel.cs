using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimsConverter.App.Services;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;

namespace SimsConverter.App.ViewModels;

public partial class ResourceInspectorViewModel : ObservableObject
{
    private readonly IPackageInspectionService _inspectionService;
    private readonly ISims3PackInspectionService? _sims3PackInspectionService;
    private readonly IResourceExportService? _exportService;
    private readonly IFilePickerService? _filePickerService;
    private readonly ITextureInspectionService? _textureInspectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InspectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportResourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSims3PackPayloadCommand))]
    private string _selectedFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InspectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportResourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSims3PackPayloadCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportResourceCommand))]
    private PackageResourceRow? _selectedResource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportSims3PackPayloadCommand))]
    private Sims3PackPayloadRow? _selectedSims3PackPayload;

    [ObservableProperty]
    private TextureResourceRow? _selectedTextureResource;

    [ObservableProperty]
    private bool _isSims3PackMode;

    [ObservableProperty]
    private string? _sims3PackTitle;

    [ObservableProperty]
    private string? _sims3PackAssetId;

    [ObservableProperty]
    private string? _sims3PackAssetType;

    [ObservableProperty]
    private string? _sims3PackRootElement;

    [ObservableProperty]
    private string? _sims3PackEncoding;

    [ObservableProperty]
    private string? _sims3PackXmlSize;

    [ObservableProperty]
    private bool _allowOverwrite;

    [ObservableProperty]
    private string _statusMessage = "Ready for package inspection.";

    public ObservableCollection<PackageResourceRow> Resources { get; } = new();
    public ObservableCollection<Sims3PackPayloadRow> Sims3PackPayloads { get; } = new();
    public ObservableCollection<TextureResourceRow> TextureResources { get; } = new();
    public ObservableCollection<ConversionIssue> Issues { get; } = new();

    public bool HasIssues => Issues.Count > 0;
    public bool HasTextureResources => TextureResources.Count > 0;
    public bool CanExtractSelectedTexture => SelectedTextureResource?.CanExtractRawPayload == true;
    public bool CanParseSelectedDdsHeader => SelectedTextureResource?.CanParseDdsHeader == true;
    public bool CanInspect => !IsBusy && !string.IsNullOrWhiteSpace(SelectedFilePath);
    public bool CanExport => !IsBusy && SelectedResource != null && !string.IsNullOrWhiteSpace(SelectedFilePath) && !IsSims3PackMode;
    public bool CanExportSims3PackPayload => !IsBusy && SelectedSims3PackPayload != null && SelectedSims3PackPayload.CanExport && !string.IsNullOrWhiteSpace(SelectedFilePath) && IsSims3PackMode;

    public ResourceInspectorViewModel(
        IPackageInspectionService inspectionService,
        IResourceExportService? exportService = null,
        IFilePickerService? filePickerService = null,
        ISims3PackInspectionService? sims3PackInspectionService = null,
        ITextureInspectionService? textureInspectionService = null)
    {
        _inspectionService = inspectionService ?? throw new ArgumentNullException(nameof(inspectionService));
        _exportService = exportService;
        _filePickerService = filePickerService;
        _sims3PackInspectionService = sims3PackInspectionService;
        _textureInspectionService = textureInspectionService;
    }

    partial void OnSelectedTextureResourceChanged(TextureResourceRow? value)
    {
        OnPropertyChanged(nameof(CanExtractSelectedTexture));
        OnPropertyChanged(nameof(CanParseSelectedDdsHeader));
    }

    [RelayCommand]
    public async Task BrowseAsync()
    {
        if (_filePickerService != null)
        {
            string? pickedPath = await _filePickerService.OpenPackageFilePickerAsync();
            if (!string.IsNullOrWhiteSpace(pickedPath))
            {
                SelectedFilePath = pickedPath;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanInspect))]
    public async Task InspectAsync(CancellationToken cancellationToken = default)
    {
        if (!CanInspect)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Inspecting package container...";
        Resources.Clear();
        Sims3PackPayloads.Clear();
        TextureResources.Clear();
        Issues.Clear();
        SelectedResource = null;
        SelectedSims3PackPayload = null;
        SelectedTextureResource = null;
        ClearSims3PackMetadata();
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasTextureResources));
        OnPropertyChanged(nameof(CanExtractSelectedTexture));
        OnPropertyChanged(nameof(CanParseSelectedDdsHeader));

        bool isSims3PackFile = SelectedFilePath.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isSims3PackFile && _sims3PackInspectionService != null)
            {
                IsSims3PackMode = true;
                Sims3PackInspectionResult s3pResult = await _sims3PackInspectionService.InspectFileAsync(SelectedFilePath, cancellationToken);

                foreach (var issue in s3pResult.Issues)
                {
                    Issues.Add(issue);
                }
                OnPropertyChanged(nameof(HasIssues));

                if (s3pResult.IsSuccess)
                {
                    Sims3PackTitle = s3pResult.Title;
                    Sims3PackAssetId = s3pResult.AssetId;
                    Sims3PackAssetType = s3pResult.AssetType;
                    Sims3PackRootElement = s3pResult.RootElementName;
                    Sims3PackEncoding = s3pResult.DeclaredEncoding;
                    Sims3PackXmlSize = s3pResult.RawXmlSizeBytes.HasValue ? $"{s3pResult.RawXmlSizeBytes.Value} B" : null;

                    foreach (var row in s3pResult.PayloadRows)
                    {
                        Sims3PackPayloads.Add(row);
                    }

                    StatusMessage = Sims3PackPayloads.Count > 0
                        ? $"Sims3Pack inspection complete. Found {Sims3PackPayloads.Count} embedded payload entries."
                        : "Sims3Pack inspection complete. Container contains 0 embedded payload entries.";
                }
                else
                {
                    StatusMessage = Issues.Count > 0
                        ? $"Sims3Pack inspection failed: {Issues[0].Message}"
                        : "Sims3Pack inspection failed. Container is invalid or corrupt.";
                }
            }
            else
            {
                IsSims3PackMode = false;
                PackageInspectionResult result = await _inspectionService.InspectFileAsync(SelectedFilePath, cancellationToken);

                foreach (var issue in result.Issues)
                {
                    Issues.Add(issue);
                }
                OnPropertyChanged(nameof(HasIssues));

                if (result.IsSuccess)
                {
                    foreach (var row in result.Resources)
                    {
                        Resources.Add(row);
                    }

                    if (_textureInspectionService != null)
                    {
                        var textureResult = _textureInspectionService.InspectPackageTextures(result);
                        foreach (var texRow in textureResult.Rows)
                        {
                            TextureResources.Add(texRow);
                        }
                        foreach (var issue in textureResult.Issues)
                        {
                            if (!Issues.Contains(issue))
                            {
                                Issues.Add(issue);
                            }
                        }
                        OnPropertyChanged(nameof(HasIssues));
                        OnPropertyChanged(nameof(HasTextureResources));
                    }

                    StatusMessage = Resources.Count > 0
                        ? $"Package inspection complete. Found {Resources.Count} resource entries ({TextureResources.Count} texture candidates)."
                        : "Package inspection complete. Container contains 0 resource entries.";
                }
                else
                {
                    StatusMessage = Issues.Count > 0
                        ? $"Package inspection failed: {Issues[0].Message}"
                        : "Package inspection failed. Container is invalid or corrupt.";
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Inspection operation was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error during inspection: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasIssues));
            OnPropertyChanged(nameof(HasTextureResources));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(CanExportSims3PackPayload));
            OnPropertyChanged(nameof(CanExtractSelectedTexture));
            OnPropertyChanged(nameof(CanParseSelectedDdsHeader));
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    public async Task ExportResourceAsync(CancellationToken cancellationToken = default)
    {
        if (!CanExport || SelectedResource == null || _exportService == null)
        {
            return;
        }

        string? destinationDir = null;
        if (_filePickerService != null)
        {
            destinationDir = await _filePickerService.OpenFolderPickerAsync();
        }

        if (string.IsNullOrWhiteSpace(destinationDir))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Exporting raw resource {SelectedResource.FormattedKey}...";

        try
        {
            var exportRequest = new SingleResourceExportRequest(
                SelectedFilePath,
                SelectedResource,
                destinationDir,
                AllowOverwrite: AllowOverwrite
            );

            PackageResourceExportResult result = await _exportService.ExportResourceAsync(exportRequest, cancellationToken);

            foreach (var issue in result.Issues)
            {
                Issues.Add(issue);
            }
            OnPropertyChanged(nameof(HasIssues));

            if (result.IsSuccess)
            {
                StatusMessage = $"Raw resource {SelectedResource.FormattedKey} exported successfully ({result.ExportedBytes} bytes).";
            }
            else
            {
                StatusMessage = result.Issues.Count > 0
                    ? $"Export failed: {result.Issues[0].Message}"
                    : "Export failed.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export operation was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error during resource export: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasIssues));
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportSims3PackPayload))]
    public async Task ExportSims3PackPayloadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanExportSims3PackPayload || SelectedSims3PackPayload == null || _sims3PackInspectionService == null)
        {
            return;
        }

        string? destinationDir = null;
        if (_filePickerService != null)
        {
            destinationDir = await _filePickerService.OpenFolderPickerAsync();
        }

        if (string.IsNullOrWhiteSpace(destinationDir))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Exporting embedded DBPF payload '{SelectedSims3PackPayload.DisplayName}'...";

        try
        {
            var request = new Sims3PackExportRequest(
                SourceSims3PackPath: SelectedFilePath,
                SelectedRow: SelectedSims3PackPayload,
                OutputDirectory: destinationDir,
                AllowOverwrite: AllowOverwrite
            );

            Sims3PackExportResult result = await _sims3PackInspectionService.ExportPayloadAsync(request, cancellationToken);

            foreach (var issue in result.Issues)
            {
                Issues.Add(issue);
            }
            OnPropertyChanged(nameof(HasIssues));

            if (result.IsSuccess)
            {
                StatusMessage = $"Embedded DBPF payload '{SelectedSims3PackPayload.DisplayName}' exported successfully to {Path.GetFileName(result.OutputFilePath)} ({result.ExportedBytes} bytes).";
            }
            else
            {
                StatusMessage = result.Issues.Count > 0
                    ? $"Payload export failed: {result.Issues[0].Message}"
                    : "Payload export failed.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Payload export operation was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error during payload export: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasIssues));
        }
    }

    private void ClearSims3PackMetadata()
    {
        Sims3PackTitle = null;
        Sims3PackAssetId = null;
        Sims3PackAssetType = null;
        Sims3PackRootElement = null;
        Sims3PackEncoding = null;
        Sims3PackXmlSize = null;
    }
}
