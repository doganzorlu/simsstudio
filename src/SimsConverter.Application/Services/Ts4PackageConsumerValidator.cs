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
using SimsConverter.Package.Services;
using SimsConverter.Textures.Contracts;
using SimsConverter.Textures.Services;

namespace SimsConverter.Application.Services;

public class Ts4PackageConsumerValidator : ITs4PackageConsumerValidator
{
    private readonly IDbpfPackageParser _dbpfParser;
    private readonly IPackageResourcePayloadReader _payloadReader;
    private readonly ITs4GeomCanonicalMeshImporter _ts4Importer;
    private readonly ITs4Rle2TextureDecoder _rle2Decoder;

    public Ts4PackageConsumerValidator(
        IDbpfPackageParser? dbpfParser = null,
        IPackageResourcePayloadReader? payloadReader = null,
        ITs4GeomCanonicalMeshImporter? ts4Importer = null,
        ITs4Rle2TextureDecoder? rle2Decoder = null)
    {
        _dbpfParser = dbpfParser ?? new DbpfPackageParser();
        _payloadReader = payloadReader ?? new PackageResourcePayloadReader();
        _ts4Importer = ts4Importer ?? new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), new CanonicalMeshValidator());
        _rle2Decoder = rle2Decoder ?? new Ts4Rle2TextureDecoder();
    }

    public async Task<Ts4ConsumerValidationResult> ValidatePackageAsync(
        string packageFilePath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ValidatePackage(packageFilePath), cancellationToken).ConfigureAwait(false);
    }

    public Ts4ConsumerValidationResult ValidatePackage(string packageFilePath)
    {
        var issues = new List<ConversionIssue>();

        if (string.IsNullOrWhiteSpace(packageFilePath) || !File.Exists(packageFilePath))
        {
            issues.Add(new ConversionIssue("VALC000", "Package file path is null or does not exist.", ConversionIssueSeverity.Error));
            return new Ts4ConsumerValidationResult(false, packageFilePath ?? string.Empty, 0, 0, 0, 0, null, issues.AsReadOnly());
        }

        var parseResult = _dbpfParser.ParseFileAsync(packageFilePath).GetAwaiter().GetResult();
        if (!parseResult.IsSuccess || parseResult.Entries == null || parseResult.Entries.Count == 0)
        {
            issues.Add(new ConversionIssue("VALC001", "DBPF package parsing failed or package contains no resource entries.", ConversionIssueSeverity.Error));
            return new Ts4ConsumerValidationResult(false, packageFilePath, 0, 0, 0, 0, null, issues.AsReadOnly());
        }

        var indexKeys = new HashSet<string>(parseResult.Entries.Select(e => e.Id.FormattedKey), StringComparer.OrdinalIgnoreCase);
        int verifiedResourceCount = 0;
        int verifiedLinkCount = 0;
        int verifiedMeshCount = 0;
        int verifiedTextureCount = 0;
        ObjectCatalogMetadata? extractedCatalogMeta = null;

        foreach (var entry in parseResult.Entries)
        {
            var pRes = _payloadReader.ReadPayload(packageFilePath, entry);
            if (!pRes.IsSuccess || pRes.Payload == null)
            {
                issues.Add(new ConversionIssue("VALC002", $"Payload extraction failed for entry {entry.Id.FormattedKey}.", ConversionIssueSeverity.Error));
                continue;
            }

            byte[] payload = pRes.Payload;
            verifiedResourceCount++;

            switch (entry.Id.TypeId)
            {
                case Ts4ResourceTypeIds.CatalogObject: // COBJ 0x319E4F1D
                    VerifyCobj(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.ObjectDefinition: // OBJD 0xC0DB5AE7
                    extractedCatalogMeta = VerifyObjd(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.Model: // MODL 0x01661233
                    VerifyModl(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.ModelLod: // MLOD 0x01D10F34
                    VerifyMlod(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.MaterialDefinition: // RMAT 0x2172D019
                    VerifyRmat(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.Geom: // GEOM 0x015A1849
                    VerifyGeom(entry, payload, issues, ref verifiedMeshCount);
                    break;

                case Ts4ResourceTypeIds.Rle2Texture: // RLE2 0x3453CF95
                    VerifyRle2(entry, payload, issues, ref verifiedTextureCount);
                    break;
            }
        }

        bool isSuccess = !issues.Any(i => i.Severity is ConversionIssueSeverity.Error or ConversionIssueSeverity.Fatal)
                         && verifiedResourceCount > 0;

        return new Ts4ConsumerValidationResult(
            IsSuccess: isSuccess,
            PackageFilePath: packageFilePath,
            VerifiedResourceCount: verifiedResourceCount,
            VerifiedGraphLinkCount: verifiedLinkCount,
            VerifiedMeshCount: verifiedMeshCount,
            VerifiedTextureCount: verifiedTextureCount,
            CatalogMetadata: extractedCatalogMeta,
            Issues: issues.AsReadOnly()
        );
    }

    private static void VerifyCobj(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 24 || Encoding.ASCII.GetString(payload, 0, 4) != "COBJ")
        {
            issues.Add(new ConversionIssue("VALC010", $"COBJ {entry.Id.FormattedKey} header magic invalid.", ConversionIssueSeverity.Error));
            return;
        }

        uint objdTypeId = BitConverter.ToUInt32(payload, 8);
        uint objdGroupId = BitConverter.ToUInt32(payload, 12);
        ulong objdInstanceId = BitConverter.ToUInt64(payload, 16);

        string objdKey = new PackageResourceId(objdTypeId, objdGroupId, objdInstanceId).FormattedKey;
        if (!indexKeys.Contains(objdKey))
        {
            issues.Add(new ConversionIssue("VALC011", $"COBJ {entry.Id.FormattedKey} references missing OBJD {objdKey}.", ConversionIssueSeverity.Error));
        }
        else
        {
            linkCount++;
        }
    }

    private static ObjectCatalogMetadata? VerifyObjd(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 24 || Encoding.ASCII.GetString(payload, 0, 4) != "OBJD")
        {
            issues.Add(new ConversionIssue("VALC020", $"OBJD {entry.Id.FormattedKey} header magic invalid.", ConversionIssueSeverity.Error));
            return null;
        }

        uint modlTypeId = BitConverter.ToUInt32(payload, 8);
        uint modlGroupId = BitConverter.ToUInt32(payload, 12);
        ulong modlInstanceId = BitConverter.ToUInt64(payload, 16);

        string modlKey = new PackageResourceId(modlTypeId, modlGroupId, modlInstanceId).FormattedKey;
        if (!indexKeys.Contains(modlKey))
        {
            issues.Add(new ConversionIssue("VALC021", $"OBJD {entry.Id.FormattedKey} references missing MODL {modlKey}.", ConversionIssueSeverity.Error));
        }
        else
        {
            linkCount++;
        }

        if (payload.Length >= 40)
        {
            uint rigTypeId = BitConverter.ToUInt32(payload, 24);
            if (rigTypeId != 0)
            {
                uint rigGroupId = BitConverter.ToUInt32(payload, 28);
                ulong rigInstanceId = BitConverter.ToUInt64(payload, 32);
                string rigKey = new PackageResourceId(rigTypeId, rigGroupId, rigInstanceId).FormattedKey;
                if (!indexKeys.Contains(rigKey))
                {
                    issues.Add(new ConversionIssue("VALC022", $"OBJD {entry.Id.FormattedKey} references missing RIG {rigKey}.", ConversionIssueSeverity.Error));
                }
                else
                {
                    linkCount++;
                }
            }
        }

        if (payload.Length >= 56)
        {
            uint rsltTypeId = BitConverter.ToUInt32(payload, 40);
            if (rsltTypeId != 0)
            {
                uint rsltGroupId = BitConverter.ToUInt32(payload, 44);
                ulong rsltInstanceId = BitConverter.ToUInt64(payload, 48);
                string rsltKey = new PackageResourceId(rsltTypeId, rsltGroupId, rsltInstanceId).FormattedKey;
                if (!indexKeys.Contains(rsltKey))
                {
                    issues.Add(new ConversionIssue("VALC023", $"OBJD {entry.Id.FormattedKey} references missing RSLT {rsltKey}.", ConversionIssueSeverity.Error));
                }
                else
                {
                    linkCount++;
                }
            }
        }

        uint placementFlags = payload.Length >= 60 ? BitConverter.ToUInt32(payload, 56) : 0x00000001;
        uint footprintHash = payload.Length >= 64 ? BitConverter.ToUInt32(payload, 60) : 0x00000000;
        uint price = payload.Length >= 68 ? BitConverter.ToUInt32(payload, 64) : 100;
        uint catalogGroup = payload.Length >= 72 ? BitConverter.ToUInt32(payload, 68) : 0;

        return new ObjectCatalogMetadata(
            Price: price,
            CatalogGroup: catalogGroup,
            PlacementFlags: placementFlags,
            FootprintHash: footprintHash,
            ObjectTitle: entry.Id.FormattedKey,
            ObjectIdentityKey: entry.Id.FormattedKey,
            IsDefaultFallback: false
        );
    }

    private static void VerifyModl(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 12 || Encoding.ASCII.GetString(payload, 0, 4) != "MODL")
        {
            issues.Add(new ConversionIssue("VALC030", $"MODL {entry.Id.FormattedKey} header magic invalid.", ConversionIssueSeverity.Error));
            return;
        }

        uint count = BitConverter.ToUInt32(payload, 8);
        int offset = 12;
        for (int i = 0; i < count; i++)
        {
            if (offset + 16 > payload.Length) break;
            uint mlodTypeId = BitConverter.ToUInt32(payload, offset);
            uint mlodGroupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong mlodInstanceId = BitConverter.ToUInt64(payload, offset + 8);
            offset += 16;

            string mlodKey = new PackageResourceId(mlodTypeId, mlodGroupId, mlodInstanceId).FormattedKey;
            if (!indexKeys.Contains(mlodKey))
            {
                issues.Add(new ConversionIssue("VALC031", $"MODL {entry.Id.FormattedKey} references missing MLOD {mlodKey}.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }
        }
    }

    private static void VerifyMlod(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 16 || Encoding.ASCII.GetString(payload, 0, 4) != "MLOD")
        {
            issues.Add(new ConversionIssue("VALC040", $"MLOD {entry.Id.FormattedKey} header magic invalid.", ConversionIssueSeverity.Error));
            return;
        }

        uint meshCount = BitConverter.ToUInt32(payload, 12);
        int offset = 16;
        for (int i = 0; i < meshCount; i++)
        {
            if (offset + 16 > payload.Length) break;
            uint geomTypeId = BitConverter.ToUInt32(payload, offset);
            uint geomGroupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong geomInstanceId = BitConverter.ToUInt64(payload, offset + 8);
            offset += 16;

            string geomKey = new PackageResourceId(geomTypeId, geomGroupId, geomInstanceId).FormattedKey;
            if (!indexKeys.Contains(geomKey))
            {
                issues.Add(new ConversionIssue("VALC041", $"MLOD {entry.Id.FormattedKey} references missing GEOM {geomKey}.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }
        }

        if (offset + 16 <= payload.Length)
        {
            uint rmatTypeId = BitConverter.ToUInt32(payload, offset);
            uint rmatGroupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong rmatInstanceId = BitConverter.ToUInt64(payload, offset + 8);
            string rmatKey = new PackageResourceId(rmatTypeId, rmatGroupId, rmatInstanceId).FormattedKey;

            if (!indexKeys.Contains(rmatKey))
            {
                issues.Add(new ConversionIssue("VALC042", $"MLOD {entry.Id.FormattedKey} references missing RMAT {rmatKey}.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }
        }
    }

    private static void VerifyRmat(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 12 || Encoding.ASCII.GetString(payload, 0, 4) != "RMAT")
        {
            issues.Add(new ConversionIssue("VALC050", $"RMAT {entry.Id.FormattedKey} header magic invalid.", ConversionIssueSeverity.Error));
            return;
        }

        uint texCount = BitConverter.ToUInt32(payload, 8);
        int offset = 12;
        for (int i = 0; i < texCount; i++)
        {
            if (offset + 16 > payload.Length) break;
            uint texTypeId = BitConverter.ToUInt32(payload, offset);
            uint texGroupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong texInstanceId = BitConverter.ToUInt64(payload, offset + 8);
            offset += 16;

            string texKey = new PackageResourceId(texTypeId, texGroupId, texInstanceId).FormattedKey;
            if (!indexKeys.Contains(texKey))
            {
                issues.Add(new ConversionIssue("VALC051", $"RMAT {entry.Id.FormattedKey} references missing RLE2 texture {texKey}.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }
        }
    }

    private void VerifyGeom(PackageResourceEntry entry, byte[] payload, List<ConversionIssue> issues, ref int meshCount)
    {
        var importRes = _ts4Importer.Import(payload, entry.Id.FormattedKey);
        if (!importRes.IsSuccess || importRes.Mesh == null)
        {
            issues.Add(new ConversionIssue("VALC060", $"GEOM {entry.Id.FormattedKey} decoding failed.", ConversionIssueSeverity.Error));
            return;
        }

        if (importRes.Mesh.Vertices.Count == 0 || importRes.Mesh.Faces.Count == 0)
        {
            issues.Add(new ConversionIssue("VALC061", $"GEOM {entry.Id.FormattedKey} contains zero vertices or faces.", ConversionIssueSeverity.Error));
            return;
        }

        meshCount++;
    }

    private void VerifyRle2(PackageResourceEntry entry, byte[] payload, List<ConversionIssue> issues, ref int texCount)
    {
        var decodeRes = _rle2Decoder.Decode(payload, entry.Id.FormattedKey);
        if (!decodeRes.IsSuccess || decodeRes.MipLevels == null || decodeRes.MipLevels.Count == 0)
        {
            issues.Add(new ConversionIssue("VALC070", $"RLE2 {entry.Id.FormattedKey} texture stream decoding failed.", ConversionIssueSeverity.Error));
            return;
        }

        if (decodeRes.Width == 0 || decodeRes.Height == 0)
        {
            issues.Add(new ConversionIssue("VALC071", $"RLE2 {entry.Id.FormattedKey} has invalid dimensions ({decodeRes.Width}x{decodeRes.Height}).", ConversionIssueSeverity.Error));
            return;
        }

        texCount++;
    }
}
