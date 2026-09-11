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

namespace SimsConverter.Application.Services;

public class Ts4ResourcePayloadCompatibilityVerifier : ITs4ResourcePayloadCompatibilityVerifier
{
    private readonly IPackageResourcePayloadReader _payloadReader;
    private readonly ITs4GeomCanonicalMeshImporter _ts4Importer;

    public Ts4ResourcePayloadCompatibilityVerifier(
        IPackageResourcePayloadReader? payloadReader = null,
        ITs4GeomCanonicalMeshImporter? ts4Importer = null)
    {
        _payloadReader = payloadReader ?? new PackageResourcePayloadReader();
        _ts4Importer = ts4Importer ?? new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), new CanonicalMeshValidator());
    }

    public async Task<Ts4PayloadCompatibilityResult> VerifyPackagePayloadsAsync(
        string packagePath,
        DbpfParseResult parseResult,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => VerifyPackagePayloads(packagePath, parseResult), cancellationToken).ConfigureAwait(false);
    }

    public Ts4PayloadCompatibilityResult VerifyPackagePayloads(
        string packagePath,
        DbpfParseResult parseResult)
    {
        var issues = new List<ConversionIssue>();

        if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
        {
            issues.Add(new ConversionIssue("VAL000", "Package path is null or file does not exist.", ConversionIssueSeverity.Error));
            return new Ts4PayloadCompatibilityResult(false, 0, 0, issues.AsReadOnly());
        }

        if (parseResult == null || !parseResult.IsSuccess || parseResult.Entries == null)
        {
            issues.Add(new ConversionIssue("VAL000", "Dbpf parse result is invalid or contains no entries.", ConversionIssueSeverity.Error));
            return new Ts4PayloadCompatibilityResult(false, 0, 0, issues.AsReadOnly());
        }

        var indexKeys = new HashSet<string>(parseResult.Entries.Select(e => e.Id.FormattedKey), StringComparer.OrdinalIgnoreCase);
        int totalVerified = 0;
        int verifiedLinkCount = 0;

        foreach (var entry in parseResult.Entries)
        {
            var pResult = _payloadReader.ReadPayload(packagePath, entry);
            if (!pResult.IsSuccess || pResult.Payload == null)
            {
                issues.Add(new ConversionIssue("VAL000", $"Failed to extract payload for resource {entry.Id.FormattedKey}.", ConversionIssueSeverity.Error));
                continue;
            }

            byte[] payload = pResult.Payload.ToArray();
            totalVerified++;

            switch (entry.Id.TypeId)
            {
                case Ts4ResourceTypeIds.CatalogObject: // COBJ 0x319E4F1D
                    VerifyCatalogObjectPayload(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.Model: // MODL 0x01661233
                    VerifyModelPayload(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.ModelLod: // MLOD 0x01D10F34
                    VerifyModelLodPayload(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.MaterialDefinition: // RMAT 0x2172D019
                    VerifyMaterialPayload(entry, payload, indexKeys, issues, ref verifiedLinkCount);
                    break;

                case Ts4ResourceTypeIds.Geom: // GEOM 0x015A1849
                    VerifyGeomPayload(entry, payload, issues);
                    break;
            }
        }

        bool isSuccess = !issues.Any(i => i.Severity is ConversionIssueSeverity.Error or ConversionIssueSeverity.Fatal);

        return new Ts4PayloadCompatibilityResult(
            IsSuccess: isSuccess,
            TotalResourcesVerified: totalVerified,
            VerifiedTgiLinkCount: verifiedLinkCount,
            Issues: issues.AsReadOnly()
        );
    }

    private static void VerifyCatalogObjectPayload(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 24 || Encoding.ASCII.GetString(payload, 0, 4) != "COBJ")
        {
            issues.Add(new ConversionIssue("VAL001", $"Catalog Object resource {entry.Id.FormattedKey} payload header is invalid (expected magic 'COBJ').", ConversionIssueSeverity.Error));
            return;
        }

        uint typeId = BitConverter.ToUInt32(payload, 8);
        uint groupId = BitConverter.ToUInt32(payload, 12);
        ulong instanceId = BitConverter.ToUInt64(payload, 16);

        string modlKey = new PackageResourceId(typeId, groupId, instanceId).FormattedKey;
        if (!indexKeys.Contains(modlKey))
        {
            issues.Add(new ConversionIssue("VAL001", $"COBJ resource {entry.Id.FormattedKey} references non-existent MODL TGI {modlKey} in package index.", ConversionIssueSeverity.Error));
        }
        else
        {
            linkCount++;
        }
    }

    private static void VerifyModelPayload(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 12 || Encoding.ASCII.GetString(payload, 0, 4) != "MODL")
        {
            issues.Add(new ConversionIssue("VAL002", $"Model resource {entry.Id.FormattedKey} payload header is invalid (expected magic 'MODL').", ConversionIssueSeverity.Error));
            return;
        }

        uint lodCount = BitConverter.ToUInt32(payload, 8);
        int offset = 12;

        for (int i = 0; i < lodCount; i++)
        {
            if (offset + 16 > payload.Length)
            {
                issues.Add(new ConversionIssue("VAL002", $"MODL resource {entry.Id.FormattedKey} payload truncated reading LOD {i}.", ConversionIssueSeverity.Error));
                break;
            }

            uint typeId = BitConverter.ToUInt32(payload, offset);
            uint groupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong instanceId = BitConverter.ToUInt64(payload, offset + 8);

            string mlodKey = new PackageResourceId(typeId, groupId, instanceId).FormattedKey;
            if (!indexKeys.Contains(mlodKey))
            {
                issues.Add(new ConversionIssue("VAL002", $"MODL resource {entry.Id.FormattedKey} references non-existent MLOD TGI {mlodKey} in package index.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }

            offset += 16;
        }
    }

    private static void VerifyModelLodPayload(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 16 || Encoding.ASCII.GetString(payload, 0, 4) != "MLOD")
        {
            issues.Add(new ConversionIssue("VAL003", $"Model LOD resource {entry.Id.FormattedKey} payload header is invalid (expected magic 'MLOD').", ConversionIssueSeverity.Error));
            return;
        }

        uint version = BitConverter.ToUInt32(payload, 4);
        uint lodIndex = BitConverter.ToUInt32(payload, 8);
        uint meshCount = BitConverter.ToUInt32(payload, 12);
        int offset = 16;

        long requiredBytesForMeshes = 16L + ((long)meshCount * 16L);
        if (payload.Length < requiredBytesForMeshes)
        {
            issues.Add(new ConversionIssue("VAL003", $"MLOD resource {entry.Id.FormattedKey} payload truncated reading {meshCount} mesh TGIs.", ConversionIssueSeverity.Error));
            return;
        }

        for (int i = 0; i < meshCount; i++)
        {
            uint typeId = BitConverter.ToUInt32(payload, offset);
            uint groupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong instanceId = BitConverter.ToUInt64(payload, offset + 8);

            string geomKey = new PackageResourceId(typeId, groupId, instanceId).FormattedKey;
            if (!indexKeys.Contains(geomKey))
            {
                issues.Add(new ConversionIssue("VAL003", $"MLOD resource {entry.Id.FormattedKey} references non-existent GEOM TGI {geomKey} in package index.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }

            offset += 16;
        }

        if (meshCount > 0)
        {
            if (offset + 16 > payload.Length)
            {
                issues.Add(new ConversionIssue("VAL003", $"MLOD resource {entry.Id.FormattedKey} payload truncated reading Material TGI.", ConversionIssueSeverity.Error));
                return;
            }

            uint typeId = BitConverter.ToUInt32(payload, offset);
            uint groupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong instanceId = BitConverter.ToUInt64(payload, offset + 8);

            string matKey = new PackageResourceId(typeId, groupId, instanceId).FormattedKey;
            if (!indexKeys.Contains(matKey))
            {
                issues.Add(new ConversionIssue("VAL003", $"MLOD resource {entry.Id.FormattedKey} references non-existent Material TGI {matKey} in package index.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }
        }
    }

    private static void VerifyMaterialPayload(PackageResourceEntry entry, byte[] payload, HashSet<string> indexKeys, List<ConversionIssue> issues, ref int linkCount)
    {
        if (payload.Length < 12 || Encoding.ASCII.GetString(payload, 0, 4) != "RMAT")
        {
            issues.Add(new ConversionIssue("VAL004", $"Material resource {entry.Id.FormattedKey} payload header is invalid (expected magic 'RMAT').", ConversionIssueSeverity.Error));
            return;
        }

        uint texCount = BitConverter.ToUInt32(payload, 8);
        int offset = 12;

        for (int i = 0; i < texCount; i++)
        {
            if (offset + 16 > payload.Length)
            {
                issues.Add(new ConversionIssue("VAL004", $"RMAT resource {entry.Id.FormattedKey} payload truncated reading texture {i}.", ConversionIssueSeverity.Error));
                break;
            }

            uint typeId = BitConverter.ToUInt32(payload, offset);
            uint groupId = BitConverter.ToUInt32(payload, offset + 4);
            ulong instanceId = BitConverter.ToUInt64(payload, offset + 8);

            string texKey = new PackageResourceId(typeId, groupId, instanceId).FormattedKey;
            if (!indexKeys.Contains(texKey))
            {
                issues.Add(new ConversionIssue("VAL004", $"Material resource {entry.Id.FormattedKey} references non-existent Texture TGI {texKey} in package index.", ConversionIssueSeverity.Error));
            }
            else
            {
                linkCount++;
            }

            offset += 16;
        }
    }

    private void VerifyGeomPayload(PackageResourceEntry entry, byte[] payload, List<ConversionIssue> issues)
    {
        var importResult = _ts4Importer.Import(payload, entry.Id.FormattedKey);
        if (!importResult.IsSuccess || importResult.Mesh == null)
        {
            issues.Add(new ConversionIssue("VAL005", $"GEOM resource {entry.Id.FormattedKey} payload is invalid or failed canonical mesh import.", ConversionIssueSeverity.Error));
            if (importResult.Issues != null)
            {
                issues.AddRange(importResult.Issues.Where(i => i.Severity is ConversionIssueSeverity.Error or ConversionIssueSeverity.Fatal));
            }
        }
    }
}
