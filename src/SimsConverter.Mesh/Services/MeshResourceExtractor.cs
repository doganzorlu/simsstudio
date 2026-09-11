using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Mesh.Services;

public class MeshResourceExtractor : IMeshResourceExtractor
{
    private readonly IPackageResourceExporter _packageResourceExporter;
    private static readonly Regex InvalidFileNameCharRegex = new(@"[\x00-\x1F\x7F\x22\x3C\x3E\x7C\x3A\x2A\x3F\x5C\x2F]", RegexOptions.Compiled);

    public MeshResourceExtractor(IPackageResourceExporter packageResourceExporter)
    {
        _packageResourceExporter = packageResourceExporter ?? throw new ArgumentNullException(nameof(packageResourceExporter));
    }

    public async Task<MeshResourceExtractResult> ExtractAsync(
        MeshResourceExtractRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return CreateFailure("", "", "MESHE000", "Mesh extraction request is null.");
        }

        string sourcePath = request.SourcePackagePath ?? "";
        string outDir = request.OutputDirectory ?? "";

        if (string.IsNullOrWhiteSpace(request.SourcePackagePath))
        {
            return CreateFailure(sourcePath, "", "MESHE000", "SourcePackagePath is null or whitespace.");
        }

        if (request.Entry == null || request.Entry.Id == null)
        {
            return CreateFailure(sourcePath, "", "MESHE000", "PackageResourceEntry or ResourceId is null.");
        }

        if (request.Classification == null || request.Classification.ResourceId == null)
        {
            return CreateFailure(sourcePath, "", "MESHE000", "MeshResourceClassification or ResourceId is null.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return CreateFailure(sourcePath, "", "MESHE000", "OutputDirectory is null or whitespace.");
        }

        // Guard 1: Must be KnownMesh
        if (request.Classification.Classification != MeshClassificationKind.KnownMesh)
        {
            return CreateFailure(
                sourcePath,
                "",
                "MESHE001",
                "Resource classification is not KnownMesh and cannot be exported as raw mesh payload."
            );
        }

        // Guard 2: ResourceId Identity Match
        if (!request.Classification.ResourceId.Equals(request.Entry.Id))
        {
            return CreateFailure(
                sourcePath,
                "",
                "MESHE004",
                $"Classification ResourceId '{request.Classification.ResourceId.FormattedKey}' does not match Entry ResourceId '{request.Entry.Id.FormattedKey}'."
            );
        }

        // Extension Mapping
        string extension = GetMeshFileExtension(request.Entry.Id.TypeId);

        // Filename Construction & Sanitization
        string defaultBaseName = $"{request.Entry.Id.TypeId:X8}_{request.Entry.Id.GroupId:X8}_{request.Entry.Id.InstanceId:X16}";
        string rawBaseName = !string.IsNullOrWhiteSpace(request.CustomFileName)
            ? request.CustomFileName
            : defaultBaseName;

        string sanitizedBaseName = InvalidFileNameCharRegex.Replace(rawBaseName, "_").Trim();
        if (string.IsNullOrWhiteSpace(sanitizedBaseName))
        {
            sanitizedBaseName = defaultBaseName;
        }

        if (!sanitizedBaseName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            sanitizedBaseName += extension;
        }

        string targetFilePath = Path.Combine(outDir, sanitizedBaseName);

        // Boundary Guards: Path Traversal & Source Identity Check
        string canonicalOutput = Path.GetFullPath(targetFilePath);
        string canonicalOutputDir = Path.GetFullPath(outDir);
        string dirWithSeparator = canonicalOutputDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? canonicalOutputDir
            : canonicalOutputDir + Path.DirectorySeparatorChar;

        if (!canonicalOutput.StartsWith(dirWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(canonicalOutput, canonicalOutputDir, StringComparison.OrdinalIgnoreCase))
        {
            return CreateFailure(
                sourcePath,
                targetFilePath,
                "MESHE008",
                "Target output file path cannot escape the output directory."
            );
        }

        string canonicalSource = Path.GetFullPath(sourcePath);
        if (string.Equals(canonicalSource, canonicalOutput, StringComparison.OrdinalIgnoreCase))
        {
            return CreateFailure(
                sourcePath,
                targetFilePath,
                "MESHE008",
                "Target output file path cannot be identical to source package file path."
            );
        }

        // Delegate raw byte export to IPackageResourceExporter
        var packageExportRequest = new PackageResourceExportRequest(
            SourcePackagePath: request.SourcePackagePath,
            Offset: request.Entry.DataOffset,
            CompressedSize: request.Entry.CompressedSize,
            OutputFilePath: targetFilePath,
            AllowOverwrite: request.AllowOverwrite
        );

        PackageResourceExportResult exportResult = await _packageResourceExporter.ExportAsync(packageExportRequest, cancellationToken);

        return new MeshResourceExtractResult(
            IsSuccess: exportResult.IsSuccess,
            SourcePackagePath: exportResult.SourcePackagePath,
            TargetFilePath: exportResult.OutputFilePath,
            ExportedSizeBytes: exportResult.ExportedBytes,
            Issues: exportResult.Issues
        );
    }

    private static string GetMeshFileExtension(uint typeId)
    {
        return typeId switch
        {
            MeshTypeIds.Ts3Geom => ".geom",
            MeshTypeIds.TsSharedModel => ".modl",
            MeshTypeIds.TsSharedModelLod => ".mlod",
            MeshTypeIds.TsSharedRig => ".rig",
            MeshTypeIds.TsSharedSlot => ".rslt",
            MeshTypeIds.TsSharedBlendGeometry => ".bgeo",
            _ => ".raw"
        };
    }

    private static MeshResourceExtractResult CreateFailure(
        string sourcePath,
        string targetPath,
        string code,
        string message)
    {
        var issue = new ConversionIssue(code, message, ConversionIssueSeverity.Error);
        return new MeshResourceExtractResult(
            IsSuccess: false,
            SourcePackagePath: sourcePath,
            TargetFilePath: targetPath,
            ExportedSizeBytes: 0,
            Issues: new[] { issue }
        );
    }
}
