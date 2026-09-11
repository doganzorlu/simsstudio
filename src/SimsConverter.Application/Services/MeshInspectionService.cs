using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Application.Services;

public class MeshInspectionService : IMeshInspectionService
{
    private readonly IPackageInspectionService _packageInspectionService;
    private readonly IMeshResourceClassifier _meshClassifier;
    private readonly ITs3GeomCanonicalMeshImporter _ts3GeomImporter;
    private readonly ITs4GeomCanonicalMeshImporter _ts4GeomImporter;
    private readonly IPackageResourcePayloadReader _payloadReader;

    public MeshInspectionService(
        IPackageInspectionService packageInspectionService,
        IMeshResourceClassifier meshClassifier,
        ITs3GeomCanonicalMeshImporter ts3GeomImporter,
        ITs4GeomCanonicalMeshImporter ts4GeomImporter,
        IPackageResourcePayloadReader payloadReader)
    {
        _packageInspectionService = packageInspectionService ?? throw new ArgumentNullException(nameof(packageInspectionService));
        _meshClassifier = meshClassifier ?? throw new ArgumentNullException(nameof(meshClassifier));
        _ts3GeomImporter = ts3GeomImporter ?? throw new ArgumentNullException(nameof(ts3GeomImporter));
        _ts4GeomImporter = ts4GeomImporter ?? throw new ArgumentNullException(nameof(ts4GeomImporter));
        _payloadReader = payloadReader ?? throw new ArgumentNullException(nameof(payloadReader));
    }

    public async Task<MeshInspectionResult> InspectPackageMeshesAsync(
        MeshInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.PackageFilePath))
        {
            return MeshInspectionResult.Failure(
                request?.PackageFilePath ?? string.Empty,
                "MESHA000",
                "Mesh inspection request or package file path is null or empty."
            );
        }

        var packageResult = await _packageInspectionService.InspectFileAsync(request.PackageFilePath, cancellationToken).ConfigureAwait(false);
        if (!packageResult.IsSuccess)
        {
            return new MeshInspectionResult(
                IsSuccess: false,
                PackageFilePath: request.PackageFilePath,
                Rows: Array.Empty<MeshResourceRow>(),
                Issues: packageResult.Issues
            );
        }

        return InspectPackageMeshes(packageResult, request.GameVersionHint);
    }

    public MeshInspectionResult InspectPackageMeshes(
        PackageInspectionResult packageInspection,
        GameVersion gameVersionHint = GameVersion.Unknown)
    {
        if (packageInspection == null)
        {
            return MeshInspectionResult.Failure(string.Empty, "MESHA000", "Package inspection result is null.");
        }

        var aggregatedIssues = new List<ConversionIssue>(packageInspection.Issues ?? Array.Empty<ConversionIssue>());
        var rows = new List<MeshResourceRow>();

        if (packageInspection.Resources != null)
        {
            foreach (var row in packageInspection.Resources)
            {
                var entry = new PackageResourceEntry(
                    new PackageResourceId(row.TypeId, row.GroupId, row.InstanceId),
                    row.Offset,
                    row.CompressedSize,
                    row.DecompressedSize,
                    row.CompressionKind,
                    0
                );

                var classification = _meshClassifier.Classify(entry, gameVersionHint);

                bool canExtractRawPayload = (classification.Classification == MeshClassificationKind.KnownMesh);
                bool canInspectCanonicalMesh = false;

                uint? vertexCount = null;
                uint? faceCount = null;
                uint? boneCount = null;
                bool hasNormals = false;
                bool hasUv0 = false;
                bool hasBoneWeights = false;
                int validationIssueCount = 0;

                var rowIssues = new List<ConversionIssue>(classification.Issues ?? Array.Empty<ConversionIssue>());

                if (classification.Classification == MeshClassificationKind.KnownMesh &&
                    (entry.Id.TypeId == MeshTypeIds.Ts3Geom || entry.Id.TypeId == MeshTypeIds.TsSharedGeom))
                {
                    var payloadResult = _payloadReader.ReadPayload(packageInspection.FilePath, entry);

                    if (payloadResult.Issues != null && payloadResult.Issues.Count > 0)
                    {
                        rowIssues.AddRange(payloadResult.Issues);
                    }

                    if (payloadResult.IsSuccess && payloadResult.Payload != null)
                    {
                        CanonicalMesh? mesh = null;
                        IReadOnlyList<ConversionIssue>? importIssues = null;
                        bool importSuccess = false;

                        if (classification.DetectedGameVersion == GameVersion.Sims4)
                        {
                            var importResult = _ts4GeomImporter.Import(payloadResult.Payload, entry.Id.FormattedKey);
                            if (importResult.IsSuccess)
                            {
                                importSuccess = true;
                                mesh = importResult.Mesh;
                                importIssues = importResult.Issues;
                            }
                            else
                            {
                                var fallbackResult = _ts3GeomImporter.Import(payloadResult.Payload, entry.Id.FormattedKey);
                                importSuccess = fallbackResult.IsSuccess;
                                mesh = fallbackResult.Mesh;
                                importIssues = fallbackResult.Issues;
                            }
                        }
                        else
                        {
                            var importResult = _ts3GeomImporter.Import(payloadResult.Payload, entry.Id.FormattedKey);
                            if (importResult.IsSuccess)
                            {
                                importSuccess = true;
                                mesh = importResult.Mesh;
                                importIssues = importResult.Issues;
                            }
                            else
                            {
                                var fallbackResult = _ts4GeomImporter.Import(payloadResult.Payload, entry.Id.FormattedKey);
                                importSuccess = fallbackResult.IsSuccess;
                                mesh = fallbackResult.Mesh;
                                importIssues = fallbackResult.Issues;
                            }
                        }

                        if (importIssues != null && importIssues.Count > 0)
                        {
                            rowIssues.AddRange(importIssues);
                        }

                        if (importSuccess && mesh != null)
                        {
                            canInspectCanonicalMesh = true;
                            vertexCount = (uint)mesh.Vertices.Count;
                            faceCount = (uint)mesh.Faces.Count;

                            var allWeights = mesh.Vertices
                                .Where(v => v != null && v.BoneWeights != null)
                                .SelectMany(v => v.BoneWeights!)
                                .ToList();

                            boneCount = allWeights.Count > 0
                                ? (uint)allWeights.Select(bw => bw.BoneIndex).Distinct().Count()
                                : 0;

                            hasNormals = mesh.Vertices.Any(v => v != null && v.Normal.HasValue);
                            hasUv0 = mesh.Vertices.Any(v => v != null && v.Uv0.HasValue);
                            hasBoneWeights = mesh.Vertices.Any(v => v != null && v.BoneWeights != null && v.BoneWeights.Count > 0);
                            validationIssueCount = importIssues != null ? importIssues.Count : 0;
                        }
                    }
                }

                if (rowIssues.Count > 0)
                {
                    aggregatedIssues.AddRange(rowIssues);
                }

                var meshRow = new MeshResourceRow(
                    FormattedKey: entry.Id.FormattedKey,
                    TypeHex: row.TypeHex,
                    GroupHex: row.GroupHex,
                    InstanceHex: row.InstanceHex,
                    DataOffset: (uint)row.Offset,
                    CompressedSize: row.CompressedSize,
                    DecompressedSize: row.DecompressedSize,
                    CompressionName: row.CompressionKind.ToString(),
                    ClassificationKind: classification.Classification,
                    RoleKind: classification.RoleKind,
                    FormatName: classification.FormatName,
                    DetectedGameVersion: classification.DetectedGameVersion,
                    CanExtractRawPayload: canExtractRawPayload,
                    CanInspectCanonicalMesh: canInspectCanonicalMesh,
                    VertexCount: vertexCount,
                    FaceCount: faceCount,
                    BoneCount: boneCount,
                    HasNormals: hasNormals,
                    HasUv0: hasUv0,
                    HasBoneWeights: hasBoneWeights,
                    ValidationIssueCount: validationIssueCount,
                    Issues: rowIssues.AsReadOnly(),
                    Entry: entry,
                    Classification: classification
                );

                rows.Add(meshRow);
            }
        }

        return new MeshInspectionResult(
            IsSuccess: packageInspection.IsSuccess,
            PackageFilePath: packageInspection.FilePath,
            Rows: rows.AsReadOnly(),
            Issues: aggregatedIssues.AsReadOnly()
        );
    }
}
