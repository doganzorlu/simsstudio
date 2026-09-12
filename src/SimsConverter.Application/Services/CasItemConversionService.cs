using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Contracts;
using SimsConverter.Textures.Services;

namespace SimsConverter.Application.Services;

public class CasItemConversionService : ICasItemConversionService
{
    private readonly IPackageInspectionService _packageService;
    private readonly IPackageResourcePayloadReader _payloadReader;
    private readonly ITs3CasPartReader _casPartReader;
    private readonly ITs4CasPartPayloadBuilder _casPartPayloadBuilder;
    private readonly ICasBoneRigRemapper _rigRemapper;
    private readonly ITs4Rle2TexturePayloadBuilder _rle2Builder;
    private readonly ITs4Rle2TextureDecoder _rle2Decoder;
    private readonly IDbpfPackageWriter _dbpfWriter;
    private readonly IDbpfPackageParser _dbpfParser;
    private readonly ITs4ResourcePayloadCompatibilityVerifier _payloadVerifier;

    public CasItemConversionService(
        IPackageInspectionService? packageService = null,
        IPackageResourcePayloadReader? payloadReader = null,
        ITs3CasPartReader? casPartReader = null,
        ITs4CasPartPayloadBuilder? casPartPayloadBuilder = null,
        ICasBoneRigRemapper? rigRemapper = null,
        ITs4Rle2TexturePayloadBuilder? rle2Builder = null,
        IDbpfPackageWriter? dbpfWriter = null,
        IDbpfPackageParser? dbpfParser = null,
        ITs4ResourcePayloadCompatibilityVerifier? payloadVerifier = null,
        ITs4Rle2TextureDecoder? rle2Decoder = null)
    {
        _dbpfParser = dbpfParser ?? new DbpfPackageParser();
        _packageService = packageService ?? new PackageInspectionService(_dbpfParser);
        _payloadReader = payloadReader ?? new PackageResourcePayloadReader();
        _casPartReader = casPartReader ?? new Ts3CasPartReader(_payloadReader);
        _casPartPayloadBuilder = casPartPayloadBuilder ?? new Ts4CasPartPayloadBuilder();
        _rigRemapper = rigRemapper ?? new CasBoneRigRemapper();
        _rle2Builder = rle2Builder ?? new Ts4Rle2TexturePayloadBuilder();
        _rle2Decoder = rle2Decoder ?? new Ts4Rle2TextureDecoder();
        _dbpfWriter = dbpfWriter ?? new DbpfPackageWriter();
        _payloadVerifier = payloadVerifier ?? new Ts4ResourcePayloadCompatibilityVerifier(_payloadReader);
    }

    public async Task<DecorativeObjectConversionResult> ConvertCasPackageAsync(
        DecorativeObjectConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var issues = new List<ConversionIssue>();
        var steps = new List<DecorativeObjectConversionStep>();

        steps.Add(new DecorativeObjectConversionStep(
            StepId: "STEP-01-PARSE-CAS",
            Title: "Parse CAS Package & Inspect Resources",
            Status: DecorativeObjectConversionStepStatus.Completed,
            Details: $"Parsing package {Path.GetFileName(request.SourcePackagePath)}",
            Issues: Array.Empty<ConversionIssue>()
        ));

        var inspectResult = await _packageService.InspectFileAsync(request.SourcePackagePath, cancellationToken).ConfigureAwait(false);
        if (!inspectResult.IsSuccess || inspectResult.Resources == null)
        {
            return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS001", "Failed to parse CAS package container.", inspectResult.Issues);
        }

        var resources = inspectResult.Resources;
        var casMeta = await _casPartReader.ReadCasPartMetadataAsync(request.SourcePackagePath, resources, Path.GetFileNameWithoutExtension(request.SourcePackagePath), cancellationToken).ConfigureAwait(false);

        // Identify GEOM & DDS/RLE2 Resources
        var geomRows = resources.Where(r => r.TypeId == Ts4ResourceTypeIds.Geom || r.TypeId == 0x015A182Cu || r.TypeId == 0x015A1849u).ToList();
        var ddsRows = resources.Where(r => r.TypeId == 0x00B2D882u || r.TypeId == Ts4ResourceTypeIds.Rle2Texture).ToList();

        string seedIdentity = Path.GetFileNameWithoutExtension(request.SourcePackagePath);
        ulong seedHash = ComputeFnv64Hash(seedIdentity);

        uint targetCaspTypeId = request.TargetGameVersion == GameVersion.Sims3
            ? Ts4ResourceTypeIds.CasPartTS3
            : Ts4ResourceTypeIds.CasPartTS4;

        uint targetTexTypeId = request.TargetGameVersion == GameVersion.Sims3
            ? 0x00B2D882u
            : Ts4ResourceTypeIds.Rle2Texture;

        var caspId = new PackageResourceId(targetCaspTypeId, 0, seedHash);
        var primaryGeomId = geomRows.Count > 0 ? geomRows[0].ToEntry().Id : new PackageResourceId(Ts4ResourceTypeIds.Geom, 0, seedHash + 1);
        var primaryTexId = ddsRows.Count > 0 ? new PackageResourceId(targetTexTypeId, ddsRows[0].GroupId, ddsRows[0].InstanceId) : new PackageResourceId(targetTexTypeId, 0, seedHash + 2);

        var plannedResources = new List<DecorativeObjectPackageWriteResourceEntry>();
        var dbpfWriteEntries = new List<DbpfPackageWriteResourceEntry>();
        var resourceLinks = new List<DecorativeObjectSourceResourceLink>();

        // 1. Build CASP Payload
        byte[] caspPayload = _casPartPayloadBuilder.BuildCasPartPayload(caspId, primaryGeomId, primaryTexId, casMeta, resourceLinks);
        plannedResources.Add(CreateWriteEntry(caspId, caspPayload));
        dbpfWriteEntries.Add(CreateDbpfEntry(caspId, caspPayload));

        // 2. Convert & Remap GEOM Meshes (Strict: No Raw Fallbacks)
        var validator = new CanonicalMeshValidator();
        var geomImporterTs3 = new Mesh.Services.Ts3GeomCanonicalMeshImporter(new Mesh.Services.Ts3GeomMetadataReader(), validator);
        var geomImporterTs4 = new Mesh.Services.Ts4GeomCanonicalMeshImporter(new Mesh.Services.Ts4GeomMetadataReader(), validator);

        foreach (var geomRow in geomRows)
        {
            var pRes = _payloadReader.ReadPayload(request.SourcePackagePath, geomRow.ToEntry());
            if (!pRes.IsSuccess || pRes.Payload == null)
            {
                return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS003", $"Failed to read payload for GEOM resource {geomRow.FormattedKey}.", pRes.Issues);
            }

            CanonicalMesh? mesh = null;
            IReadOnlyList<ConversionIssue>? importIssues = null;

            var importResult3 = geomImporterTs3.Import(pRes.Payload, geomRow.FormattedKey);
            if (importResult3.IsSuccess && importResult3.Mesh != null)
            {
                mesh = importResult3.Mesh;
            }
            else
            {
                var importResult4 = geomImporterTs4.Import(pRes.Payload, geomRow.FormattedKey);
                if (importResult4.IsSuccess && importResult4.Mesh != null)
                {
                    mesh = importResult4.Mesh;
                }
                else
                {
                    importIssues = importResult4.Issues ?? importResult3.Issues;
                }
            }

            if (mesh == null)
            {
                return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS003", $"GEOM mesh conversion failed for resource {geomRow.FormattedKey}.", importIssues);
            }

            var remappedMesh = _rigRemapper.RemapSkeletonBones(mesh);
            byte[] geomPayload = Mesh.Services.Ts4GeomPayloadBuilder.BuildGeomPayload(
                geomRow.ToEntry().Id,
                new PackageResourceId(Ts4ResourceTypeIds.MaterialDefinition, 0, geomRow.InstanceId),
                remappedMesh
            );

            plannedResources.Add(CreateWriteEntry(geomRow.ToEntry().Id, geomPayload));
            dbpfWriteEntries.Add(CreateDbpfEntry(geomRow.ToEntry().Id, geomPayload));
        }

        // 3. Convert Textures (Strict: No Raw Fallbacks)
        foreach (var ddsRow in ddsRows)
        {
            var pRes = _payloadReader.ReadPayload(request.SourcePackagePath, ddsRow.ToEntry());
            if (!pRes.IsSuccess || pRes.Payload == null)
            {
                return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS004", $"Failed to read payload for texture resource {ddsRow.FormattedKey}.", pRes.Issues);
            }

            byte[] outputTexPayload;
            if (request.TargetGameVersion == GameVersion.Sims3)
            {
                if (pRes.Payload.Length >= 4 && Encoding.ASCII.GetString(pRes.Payload, 0, 4) == "RLE2")
                {
                    var decodeRes = _rle2Decoder.Decode(pRes.Payload, ddsRow.FormattedKey);
                    if (!decodeRes.IsSuccess || decodeRes.DecodedDdsPayload == null)
                    {
                        return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS004", $"RLE2 texture decoding to DDS failed for resource {ddsRow.FormattedKey}.", decodeRes.Issues);
                    }
                    outputTexPayload = decodeRes.DecodedDdsPayload;
                }
                else
                {
                    outputTexPayload = pRes.Payload;
                }
            }
            else
            {
                if (ddsRow.TypeId == Ts4ResourceTypeIds.Rle2Texture && pRes.Payload.Length >= 4 && Encoding.ASCII.GetString(pRes.Payload, 0, 4) == "RLE2")
                {
                    outputTexPayload = pRes.Payload;
                }
                else
                {
                    var rle2Res = _rle2Builder.BuildPayload(pRes.Payload, ddsRow.FormattedKey);
                    if (!rle2Res.IsSuccess || rle2Res.Payload == null)
                    {
                        return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS004", $"RLE2 texture conversion failed for resource {ddsRow.FormattedKey}.", rle2Res.Issues);
                    }
                    outputTexPayload = rle2Res.Payload;
                }
            }

            var outputTexId = new PackageResourceId(targetTexTypeId, ddsRow.GroupId, ddsRow.InstanceId);
            plannedResources.Add(CreateWriteEntry(outputTexId, outputTexPayload));
            dbpfWriteEntries.Add(CreateDbpfEntry(outputTexId, outputTexPayload));
        }

        // 4. Pass-through remaining whitelist resources (STBL, THUM, ICON)
        foreach (var otherRow in resources)
        {
            if (DecorativeObjectConversionCapabilityService.PassThroughTypeIds.Contains(otherRow.TypeId))
            {
                var pRes = _payloadReader.ReadPayload(request.SourcePackagePath, otherRow.ToEntry());
                if (pRes.IsSuccess && pRes.Payload != null)
                {
                    plannedResources.Add(CreateWriteEntry(otherRow.ToEntry().Id, pRes.Payload));
                    dbpfWriteEntries.Add(CreateDbpfEntry(otherRow.ToEntry().Id, pRes.Payload));
                }
            }
        }

        plannedResources.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));

        var plan = new DecorativeObjectConversionPlan(
            SourcePackagePath: request.SourcePackagePath,
            TargetOutputPath: request.TargetOutputPath,
            TargetGameVersion: request.TargetGameVersion,
            TotalMeshCandidateCount: geomRows.Count,
            TotalTextureCandidateCount: ddsRows.Count,
            ConvertableMeshCount: geomRows.Count,
            ValidTextureCount: ddsRows.Count,
            MeshCandidates: Array.Empty<MeshResourceRow>(),
            TextureCandidates: Array.Empty<TextureResourceRow>(),
            Steps: steps.AsReadOnly(),
            IsFeasible: true
        );

        // Staging Temp File Strategy: Write to temporary path first before validating
        string stagingTempPath = request.TargetOutputPath + ".staging." + Guid.NewGuid().ToString("N") + ".package";

        void CleanupStaging()
        {
            if (File.Exists(stagingTempPath))
            {
                try { File.Delete(stagingTempPath); } catch { }
            }
        }

        try
        {
            var writeResult = await _dbpfWriter.WritePackageAsync(request.SourcePackagePath, stagingTempPath, dbpfWriteEntries, cancellationToken).ConfigureAwait(false);
            if (!writeResult.IsSuccess)
            {
                CleanupStaging();
                return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS002", "Failed to write staging CAS package.", writeResult.Issues);
            }

            // Post-write consumer validation
            var parseResult = await _dbpfParser.ParseFileAsync(stagingTempPath, cancellationToken).ConfigureAwait(false);
            if (!parseResult.IsSuccess)
            {
                CleanupStaging();
                return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS005", "Staging CAS package failed DBPF container parsing.", parseResult.Issues);
            }

            var compatResult = await _payloadVerifier.VerifyPackagePayloadsAsync(stagingTempPath, parseResult, cancellationToken).ConfigureAwait(false);
            if (!compatResult.IsSuccess)
            {
                CleanupStaging();
                return DecorativeObjectConversionResult.Failure(request.SourcePackagePath, request.TargetOutputPath, "CAS006", "Staging CAS package failed payload compatibility verification.", compatResult.Issues);
            }

            // Atomic Commit Strategy: Move fully verified staging file to target path, preserving any pre-existing file on failure
            File.Move(stagingTempPath, request.TargetOutputPath, overwrite: true);

            steps.Add(new DecorativeObjectConversionStep(
                StepId: "STEP-02-COMMIT-CAS",
                Title: "Staging Post-Write Validation & Atomic Commit",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: $"Staging package parsed & verified ({compatResult.TotalResourcesVerified} verified resources, {compatResult.VerifiedTgiLinkCount} verified graph links). Atomic commit to target path complete.",
                Issues: Array.Empty<ConversionIssue>()
            ));

            var updatedPlan = plan with { Steps = steps.AsReadOnly() };

            return new DecorativeObjectConversionResult(
                IsSuccess: true,
                SourcePackagePath: request.SourcePackagePath,
                TargetOutputPath: request.TargetOutputPath,
                Plan: updatedPlan,
                Issues: issues.AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            CleanupStaging();
            return DecorativeObjectConversionResult.Failure(
                request.SourcePackagePath,
                request.TargetOutputPath,
                "CAS999",
                $"Unexpected exception during CAS conversion execution: {ex.Message}"
            );
        }
    }

    private static ulong ComputeFnv64Hash(string text)
    {
        const ulong fnvOffsetBasis = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        ulong hash = fnvOffsetBasis;

        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }

    private static DecorativeObjectPackageWriteResourceEntry CreateWriteEntry(PackageResourceId id, byte[] payload)
    {
        uint size = (uint)payload.Length;
        return new DecorativeObjectPackageWriteResourceEntry(
            ResourceId: id,
            FormattedKey: id.FormattedKey,
            Payload: payload,
            CompressionKind: PackageCompressionKind.None,
            DecompressedSize: size,
            CompressedSize: size
        );
    }

    private static DbpfPackageWriteResourceEntry CreateDbpfEntry(PackageResourceId id, byte[] payload)
    {
        uint size = (uint)payload.Length;
        return new DbpfPackageWriteResourceEntry(
            ResourceId: id,
            Payload: payload,
            CompressionKind: PackageCompressionKind.None,
            DecompressedSize: size
        );
    }
}

