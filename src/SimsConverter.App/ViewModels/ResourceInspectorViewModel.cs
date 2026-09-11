using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimsConverter.App.Services;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Services;
using SimsConverter.Textures.Services;

namespace SimsConverter.App.ViewModels;

public partial class ResourceInspectorViewModel : ObservableObject
{
    private readonly IPackageInspectionService _inspectionService;
    private readonly ISims3PackInspectionService? _sims3PackInspectionService;
    private readonly IResourceExportService? _exportService;
    private readonly IFilePickerService? _filePickerService;
    private readonly ITextureInspectionService? _textureInspectionService;
    private readonly IMeshInspectionService? _meshInspectionService;
    private readonly IDecorativeObjectConversionService? _conversionService;
    private readonly IDecorativeObjectConversionCapabilityService _capabilityService;

    private PackageInspectionResult? _lastPackageInspectionResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InspectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportResourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSims3PackPayloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private string _selectedFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InspectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportResourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSims3PackPayloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(InspectConvertedPackageCommand))]
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
    private MeshResourceRow? _selectedMeshResource;

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private GameVersion _targetGameVersion = GameVersion.Sims4;

    public ObservableCollection<TargetGameVersionOption> TargetGameVersionOptions { get; } = new()
    {
        new TargetGameVersionOption(GameVersion.Sims4, "TS3 -> TS4 (Sims 4 Target)"),
        new TargetGameVersionOption(GameVersion.Sims3, "TS4 -> TS3 (Sims 3 Target)")
    };

    [ObservableProperty]
    private TargetGameVersionOption? _selectedTargetGameVersionOption;

    public string ConversionDirectionText => TargetGameVersion == GameVersion.Sims3 ? "TS4 -> TS3" : "TS3 -> TS4";
    public string ConvertButtonContent => $"Convert {ConversionDirectionText}";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private string _targetOutputPath = string.Empty;

    [ObservableProperty]
    private bool _hasConversionResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InspectConvertedPackageCommand))]
    private bool _isConversionSuccess;

    [ObservableProperty]
    private int _conversionTotalResourceCount;

    [ObservableProperty]
    private int _conversionVerifiedLinkCount;

    [ObservableProperty]
    private int _conversionMeshCount;

    [ObservableProperty]
    private int _conversionTextureCount;

    [ObservableProperty]
    private int _conversionRigCount;

    [ObservableProperty]
    private int _conversionRsltCount;

    [ObservableProperty]
    private long _conversionTotalPayloadBytes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InspectConvertedPackageCommand))]
    private string? _lastConvertedPackagePath;

    [ObservableProperty]
    private string? _conversionObjectTitle;

    [ObservableProperty]
    private uint _conversionCatalogPrice;

    [ObservableProperty]
    private string? _conversionPlacementFlagsHex;

    [ObservableProperty]
    private string? _conversionFootprintHashHex;

    [ObservableProperty]
    private bool _conversionIsCatalogFallback;

    [ObservableProperty]
    private DecorativeObjectConversionCapabilityMatrix? _capabilityMatrix;

    [ObservableProperty]
    private int _capabilitySupportedCount;

    [ObservableProperty]
    private int _capabilityPassThroughCount;

    [ObservableProperty]
    private int _capabilityUnsupportedCount;

    [ObservableProperty]
    private bool _hasCapabilityMatrix;

    [ObservableProperty]
    private bool _hasCapabilityWarnings;

    public ObservableCollection<PackageResourceRow> Resources { get; } = new();
    public ObservableCollection<Sims3PackPayloadRow> Sims3PackPayloads { get; } = new();
    public ObservableCollection<TextureResourceRow> TextureResources { get; } = new();
    public ObservableCollection<MeshResourceRow> MeshResources { get; } = new();
    public ObservableCollection<ConversionIssue> Issues { get; } = new();
    public ObservableCollection<DecorativeObjectConversionStep> ConversionSteps { get; } = new();
    public ObservableCollection<DecorativeObjectSourceResourceLink> ConversionResourceLinks { get; } = new();
    public ObservableCollection<ConversionCapabilityEntry> CapabilityEntries { get; } = new();
    public ObservableCollection<ConversionIssue> CapabilityWarnings { get; } = new();

    public bool HasIssues => Issues.Count > 0;
    public bool HasTextureResources => TextureResources.Count > 0;
    public bool HasMeshResources => MeshResources.Count > 0;
    public bool CanExtractSelectedTexture => SelectedTextureResource?.CanExtractRawPayload == true;
    public bool CanParseSelectedDdsHeader => SelectedTextureResource?.CanParseDdsHeader == true;
    public bool CanInspectSelectedMesh => SelectedMeshResource?.CanInspectCanonicalMesh == true;
    public bool CanInspect => !IsBusy && !string.IsNullOrWhiteSpace(SelectedFilePath);
    public bool CanExport => !IsBusy && SelectedResource != null && !string.IsNullOrWhiteSpace(SelectedFilePath) && !IsSims3PackMode;
    public bool CanExportSims3PackPayload => !IsBusy && SelectedSims3PackPayload != null && SelectedSims3PackPayload.CanExport && !string.IsNullOrWhiteSpace(SelectedFilePath) && IsSims3PackMode;
    public bool CanConvert
    {
        get
        {
            if (IsBusy || string.IsNullOrWhiteSpace(SelectedFilePath) || string.IsNullOrWhiteSpace(TargetOutputPath))
            {
                return false;
            }

            if (IsSims3PackMode)
            {
                if (SelectedSims3PackPayload != null)
                {
                    return SelectedSims3PackPayload.CanExport;
                }
                return Sims3PackPayloads.Any(p => p.CanExport);
            }

            // Capability Guard Check: If preflight matrix has been evaluated and 0 supported resources exist, block conversion!
            if (HasCapabilityMatrix && CapabilitySupportedCount == 0)
            {
                return false;
            }

            return true;
        }
    }
    public bool CanInspectConvertedPackage => !IsBusy && IsConversionSuccess && !string.IsNullOrWhiteSpace(LastConvertedPackagePath) && File.Exists(LastConvertedPackagePath);

    public ResourceInspectorViewModel(
        IPackageInspectionService inspectionService,
        IResourceExportService? exportService = null,
        IFilePickerService? filePickerService = null,
        ISims3PackInspectionService? sims3PackInspectionService = null,
        ITextureInspectionService? textureInspectionService = null,
        IMeshInspectionService? meshInspectionService = null,
        IDecorativeObjectConversionService? conversionService = null,
        IDecorativeObjectConversionCapabilityService? capabilityService = null)
    {
        _inspectionService = inspectionService ?? throw new ArgumentNullException(nameof(inspectionService));
        _exportService = exportService;
        _filePickerService = filePickerService;
        _sims3PackInspectionService = sims3PackInspectionService;
        _textureInspectionService = textureInspectionService;
        _meshInspectionService = meshInspectionService;
        _conversionService = conversionService;
        _capabilityService = capabilityService ?? new DecorativeObjectConversionCapabilityService();
        _selectedTargetGameVersionOption = TargetGameVersionOptions[0];
    }

    public void UpdateCapabilityMatrixPreflight(PackageInspectionResult? packageResult)
    {
        CapabilityEntries.Clear();
        CapabilityWarnings.Clear();

        if (packageResult == null || packageResult.Resources == null || packageResult.Resources.Count == 0)
        {
            CapabilityMatrix = null;
            CapabilitySupportedCount = 0;
            CapabilityPassThroughCount = 0;
            CapabilityUnsupportedCount = 0;
            HasCapabilityMatrix = false;
            HasCapabilityWarnings = false;
            OnPropertyChanged(nameof(CanConvert));
            return;
        }

        GameVersion sourceVersion = IsSims3PackMode ? GameVersion.Sims3 : (TargetGameVersion == GameVersion.Sims3 ? GameVersion.Sims4 : GameVersion.Sims3);
        var matrix = _capabilityService.EvaluateCapability(packageResult, sourceVersion, TargetGameVersion);
        CapabilityMatrix = matrix;
        CapabilitySupportedCount = matrix.SupportedResourceCount;
        CapabilityPassThroughCount = matrix.PassThroughResourceCount;
        CapabilityUnsupportedCount = matrix.UnsupportedResourceCount;
        HasCapabilityMatrix = true;

        foreach (var entry in matrix.Entries)
        {
            CapabilityEntries.Add(entry);
        }

        foreach (var issue in matrix.Issues)
        {
            if (issue.Code == "CAPA001" || issue.Code == "CAPA002")
            {
                CapabilityWarnings.Add(issue);
                if (!Issues.Contains(issue))
                {
                    Issues.Add(issue);
                }
            }
        }

        HasCapabilityWarnings = CapabilityWarnings.Count > 0;
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(CanConvert));
    }

    partial void OnSelectedSims3PackPayloadChanged(Sims3PackPayloadRow? value)
    {
        OnPropertyChanged(nameof(CanExportSims3PackPayload));
        OnPropertyChanged(nameof(CanConvert));
    }

    partial void OnSelectedTextureResourceChanged(TextureResourceRow? value)
    {
        OnPropertyChanged(nameof(CanExtractSelectedTexture));
        OnPropertyChanged(nameof(CanParseSelectedDdsHeader));
    }

    partial void OnSelectedMeshResourceChanged(MeshResourceRow? value)
    {
        OnPropertyChanged(nameof(CanInspectSelectedMesh));
    }

    partial void OnSelectedTargetGameVersionOptionChanged(TargetGameVersionOption? value)
    {
        if (value != null && TargetGameVersion != value.Version)
        {
            TargetGameVersion = value.Version;
        }
    }

    partial void OnTargetGameVersionChanged(GameVersion value)
    {
        OnPropertyChanged(nameof(ConversionDirectionText));
        OnPropertyChanged(nameof(ConvertButtonContent));

        if (SelectedTargetGameVersionOption?.Version != value)
        {
            SelectedTargetGameVersionOption = TargetGameVersionOptions.FirstOrDefault(o => o.Version == value) ?? TargetGameVersionOptions[0];
        }

        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            try
            {
                string dir = Path.GetDirectoryName(SelectedFilePath) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(SelectedFilePath);
                string suffix = value == GameVersion.Sims3 ? "_ts3.package" : "_ts4.package";
                TargetOutputPath = string.IsNullOrEmpty(dir) ? $"{name}{suffix}" : Path.Combine(dir, $"{name}{suffix}");
            }
            catch
            {
                // Ignore invalid path syntax
            }
        }

        if (_lastPackageInspectionResult != null)
        {
            UpdateCapabilityMatrixPreflight(_lastPackageInspectionResult);
        }
    }

    partial void OnSelectedFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(CanInspect));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanExportSims3PackPayload));
        OnPropertyChanged(nameof(CanConvert));

        string defaultSuffix = TargetGameVersion == GameVersion.Sims3 ? "_ts3.package" : "_ts4.package";
        if (!string.IsNullOrWhiteSpace(value) && (string.IsNullOrWhiteSpace(TargetOutputPath) || TargetOutputPath.EndsWith("_ts4.package", StringComparison.OrdinalIgnoreCase) || TargetOutputPath.EndsWith("_ts3.package", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                string dir = Path.GetDirectoryName(value) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(value);
                TargetOutputPath = string.IsNullOrEmpty(dir) ? $"{name}{defaultSuffix}" : Path.Combine(dir, $"{name}{defaultSuffix}");
            }
            catch
            {
                // Ignore invalid path syntax
            }
        }
    }

    partial void OnTargetOutputPathChanged(string value)
    {
        OnPropertyChanged(nameof(CanConvert));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInspect));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanExportSims3PackPayload));
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanInspectConvertedPackage));
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
        MeshResources.Clear();
        Issues.Clear();
        SelectedResource = null;
        SelectedSims3PackPayload = null;
        SelectedTextureResource = null;
        SelectedMeshResource = null;
        ClearSims3PackMetadata();
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasTextureResources));
        OnPropertyChanged(nameof(HasMeshResources));
        OnPropertyChanged(nameof(CanExtractSelectedTexture));
        OnPropertyChanged(nameof(CanParseSelectedDdsHeader));
        OnPropertyChanged(nameof(CanInspectSelectedMesh));

        bool isSims3PackFile = SelectedFilePath.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isSims3PackFile && _sims3PackInspectionService != null)
            {
                IsSims3PackMode = true;
                TargetGameVersion = GameVersion.Sims4;
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

                    int validDbpfCount = 0;
                    int invalidDbpfCount = 0;

                    foreach (var row in s3pResult.PayloadRows)
                    {
                        Sims3PackPayloads.Add(row);
                        if (row.Kind == SimsConverter.Domain.Enums.Sims3PackPayloadKind.DbpfPackage.ToString())
                        {
                            validDbpfCount++;
                        }
                        else if (row.Kind == SimsConverter.Domain.Enums.Sims3PackPayloadKind.InvalidDbpfPackage.ToString())
                        {
                            invalidDbpfCount++;
                        }
                    }

                    StatusMessage = Sims3PackPayloads.Count > 0
                        ? $"Sims3Pack inspection complete. Found {Sims3PackPayloads.Count} embedded payload entries ({validDbpfCount} valid DBPF package{(validDbpfCount == 1 ? "" : "s")}, {invalidDbpfCount} invalid DBPF candidate{(invalidDbpfCount == 1 ? "" : "s")})."
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

                    // COBJ/MODL/MLOD are shared by TS3 and TS4 packages. Use TS4-specific
                    // definition/material markers so TS3 embedded packages are not reversed.
                    bool isTs4Source = result.Resources.Any(r =>
                        r.TypeId == 0xC0DB5AE7u || // TS4 OBJD
                        r.TypeId == 0x2172D019u || // TS4 RMAT
                        r.TypeId == 0x2BC04EDFu || // TS4 LRLE
                        r.TypeId == 0x3453CF95u || // TS4 RLE2
                        r.TypeId == 0x2F7D0004u);  // TS4 PNG image
                    TargetGameVersion = isTs4Source ? GameVersion.Sims3 : GameVersion.Sims4;

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

                    if (_meshInspectionService != null)
                    {
                        var meshResult = _meshInspectionService.InspectPackageMeshes(result);
                        foreach (var meshRow in meshResult.Rows)
                        {
                            MeshResources.Add(meshRow);
                        }
                        foreach (var issue in meshResult.Issues)
                        {
                            if (!Issues.Contains(issue))
                            {
                                Issues.Add(issue);
                            }
                        }
                        OnPropertyChanged(nameof(HasIssues));
                        OnPropertyChanged(nameof(HasMeshResources));
                    }

                    int knownTextureCandidates = 0;
                    foreach (var t in TextureResources)
                    {
                        if (t.ClassificationKind == SimsConverter.Domain.Enums.TextureClassificationKind.KnownTexture)
                        {
                            knownTextureCandidates++;
                        }
                    }

                    int knownMeshCandidates = 0;
                    foreach (var m in MeshResources)
                    {
                        if (m.ClassificationKind == SimsConverter.Domain.Enums.MeshClassificationKind.KnownMesh)
                        {
                            knownMeshCandidates++;
                        }
                    }

                    _lastPackageInspectionResult = result;
                    UpdateCapabilityMatrixPreflight(result);

                    StatusMessage = Resources.Count > 0
                        ? $"Package inspection complete. Found {Resources.Count} resource entries ({knownTextureCandidates} texture candidates, {knownMeshCandidates} mesh candidates)."
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
            OnPropertyChanged(nameof(HasMeshResources));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(CanExportSims3PackPayload));
            OnPropertyChanged(nameof(CanExtractSelectedTexture));
            OnPropertyChanged(nameof(CanParseSelectedDdsHeader));
            OnPropertyChanged(nameof(CanInspectSelectedMesh));
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

    [RelayCommand]
    public async Task BrowseTargetOutputAsync()
    {
        if (_filePickerService != null)
        {
            string? pickedPath = await _filePickerService.SavePackageFilePickerAsync();
            if (!string.IsNullOrWhiteSpace(pickedPath))
            {
                TargetOutputPath = pickedPath;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    public async Task ConvertAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConvert)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Executing {ConversionDirectionText} conversion...";
        HasConversionResult = false;
        IsConversionSuccess = false;
        LastConvertedPackagePath = null;
        ConversionTotalResourceCount = 0;
        ConversionVerifiedLinkCount = 0;
        ConversionMeshCount = 0;
        ConversionTextureCount = 0;
        ConversionRigCount = 0;
        ConversionRsltCount = 0;
        ConversionTotalPayloadBytes = 0;
        ConversionSteps.Clear();
        ConversionResourceLinks.Clear();
        Issues.Clear();

        string sourcePackagePathToConvert = SelectedFilePath;
        string? tempExtractedPackagePath = null;

        try
        {
            if (IsSims3PackMode)
            {
                if (_sims3PackInspectionService == null)
                {
                    StatusMessage = "Sims3Pack inspection service is not available.";
                    return;
                }

                Sims3PackPayloadRow? targetPayloadRow = SelectedSims3PackPayload;
                if (targetPayloadRow == null || !targetPayloadRow.CanExport)
                {
                    targetPayloadRow = Sims3PackPayloads.FirstOrDefault(p => p.CanExport);
                }

                if (targetPayloadRow == null || !targetPayloadRow.CanExport)
                {
                    var issue = new ConversionIssue("CONVA005", "No valid embedded DBPF package payload found in Sims3Pack for conversion.", ConversionIssueSeverity.Error);
                    Issues.Add(issue);
                    HasConversionResult = true;
                    IsConversionSuccess = false;
                    StatusMessage = "Conversion failed: No valid embedded DBPF package payload found in Sims3Pack.";
                    return;
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "SimsStudio_Sims3Pack_Temp_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                var exportRequest = new Sims3PackExportRequest(
                    SourceSims3PackPath: SelectedFilePath,
                    SelectedRow: targetPayloadRow,
                    OutputDirectory: tempDir,
                    AllowOverwrite: true
                );

                var exportResult = await _sims3PackInspectionService.ExportPayloadAsync(exportRequest, cancellationToken);
                if (!exportResult.IsSuccess || string.IsNullOrWhiteSpace(exportResult.OutputFilePath) || !File.Exists(exportResult.OutputFilePath))
                {
                    foreach (var issue in exportResult.Issues)
                    {
                        Issues.Add(issue);
                    }
                    HasConversionResult = true;
                    IsConversionSuccess = false;
                    StatusMessage = exportResult.Issues.Count > 0
                        ? $"Conversion failed during Sims3Pack payload extraction: {exportResult.Issues[0].Message}"
                        : "Conversion failed during Sims3Pack payload extraction.";
                    return;
                }

                tempExtractedPackagePath = exportResult.OutputFilePath;
                sourcePackagePathToConvert = tempExtractedPackagePath;
            }

            if (_conversionService == null)
            {
                StatusMessage = "Conversion service is not available.";
                return;
            }

            var request = new DecorativeObjectConversionRequest(
                sourcePackagePathToConvert,
                TargetOutputPath,
                TargetGameVersion,
                SourceGameVersion: IsSims3PackMode ? GameVersion.Sims3 : (TargetGameVersion == GameVersion.Sims3 ? GameVersion.Sims4 : GameVersion.Sims3)
            );

            DecorativeObjectConversionResult result = await _conversionService.ExecuteConversionAsync(request, cancellationToken);

            foreach (var issue in result.Issues)
            {
                if (!Issues.Contains(issue))
                {
                    Issues.Add(issue);
                }
            }

            if (result.Plan?.Steps != null)
            {
                foreach (var step in result.Plan.Steps)
                {
                    ConversionSteps.Add(step);
                }
            }

            if (result.Plan?.InputBundle?.ResourceLinks != null)
            {
                foreach (var link in result.Plan.InputBundle.ResourceLinks)
                {
                    ConversionResourceLinks.Add(link);
                }
            }
            else if (result.Plan?.SourceGraph?.ResourceLinks != null)
            {
                foreach (var link in result.Plan.SourceGraph.ResourceLinks)
                {
                    ConversionResourceLinks.Add(link);
                }
            }

            HasConversionResult = true;

            if (result.IsSuccess)
            {
                IsConversionSuccess = true;
                LastConvertedPackagePath = result.TargetOutputPath;

                if (result.Plan?.InputBundle != null)
                {
                    ConversionMeshCount = result.Plan.InputBundle.MeshBundles?.Count ?? 0;
                    ConversionTextureCount = result.Plan.InputBundle.TextureAssets?.Count ?? 0;
                    ConversionRigCount = result.Plan.InputBundle.RigResources?.Count ?? 0;
                    ConversionRsltCount = result.Plan.InputBundle.RsltResources?.Count ?? 0;

                    long meshBytes = result.Plan.InputBundle.MeshBundles != null ? result.Plan.InputBundle.MeshBundles.Sum(m => (long)(m.RawPayload?.Count ?? 0)) : 0;
                    long texBytes = result.Plan.InputBundle.TextureAssets != null ? result.Plan.InputBundle.TextureAssets.Sum(t => (long)(t.RawPayload?.Count ?? 0)) : 0;
                    long rigBytes = result.Plan.InputBundle.RigResources != null ? result.Plan.InputBundle.RigResources.Sum(r => (long)r.CompressedSize) : 0;
                    long rsltBytes = result.Plan.InputBundle.RsltResources != null ? result.Plan.InputBundle.RsltResources.Sum(r => (long)r.CompressedSize) : 0;
                    ConversionTotalPayloadBytes = meshBytes + texBytes + rigBytes + rsltBytes;

                    ConversionTotalResourceCount = 5 + ConversionMeshCount + ConversionTextureCount + ConversionRigCount + ConversionRsltCount;

                    if (result.Plan.InputBundle.CatalogMetadata != null)
                    {
                        var meta = result.Plan.InputBundle.CatalogMetadata;
                        ConversionObjectTitle = meta.ObjectTitle;
                        ConversionCatalogPrice = meta.Price;
                        ConversionPlacementFlagsHex = $"0x{meta.PlacementFlags:X8}";
                        ConversionFootprintHashHex = $"0x{meta.FootprintHash:X8}";
                        ConversionIsCatalogFallback = meta.IsDefaultFallback;
                    }
                }
                else
                {
                    ConversionTotalResourceCount = 5;
                }

                ConversionVerifiedLinkCount = ConversionResourceLinks.Count > 0
                    ? ConversionResourceLinks.Count
                    : (result.Plan?.Steps.Count ?? 0);

                int warningCount = result.Issues.Count(i => i.Severity == ConversionIssueSeverity.Warning);
                string targetFileName = Path.GetFileName(result.TargetOutputPath);
                string targetVersionLabel = TargetGameVersion == GameVersion.Sims4 ? "TS4" : "TS3";

                StatusMessage = warningCount > 0
                    ? $"Conversion completed successfully with {warningCount} warning(s). {targetVersionLabel} package written to {targetFileName} ({ConversionTotalResourceCount} resources, {ConversionVerifiedLinkCount} verified TGI links)."
                    : $"Conversion completed successfully. {targetVersionLabel} package written to {targetFileName} ({ConversionTotalResourceCount} resources, {ConversionVerifiedLinkCount} verified TGI links).";
            }
            else
            {
                IsConversionSuccess = false;
                LastConvertedPackagePath = null;
                StatusMessage = result.Issues.Count > 0
                    ? $"Conversion failed: {result.Issues[0].Message}"
                    : "Conversion failed due to invalid bundle or write plan.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Conversion operation was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error during conversion: {ex.Message}";
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempExtractedPackagePath))
            {
                try
                {
                    if (File.Exists(tempExtractedPackagePath))
                    {
                        File.Delete(tempExtractedPackagePath);
                    }
                    string? parentDir = Path.GetDirectoryName(tempExtractedPackagePath);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir) && parentDir.Contains("SimsStudio_Sims3Pack_Temp_"))
                    {
                        Directory.Delete(parentDir, true);
                    }
                }
                catch
                {
                    // Ignore temp file cleanup exceptions
                }
            }

            IsBusy = false;
            OnPropertyChanged(nameof(HasIssues));
            OnPropertyChanged(nameof(CanConvert));
            OnPropertyChanged(nameof(CanInspectConvertedPackage));
        }
    }

    [RelayCommand(CanExecute = nameof(CanInspectConvertedPackage))]
    public async Task InspectConvertedPackageAsync(CancellationToken cancellationToken = default)
    {
        if (!CanInspectConvertedPackage || string.IsNullOrWhiteSpace(LastConvertedPackagePath))
        {
            return;
        }

        SelectedFilePath = LastConvertedPackagePath;
        await InspectAsync(cancellationToken);
    }
}

public record TargetGameVersionOption(GameVersion Version, string DisplayName);
