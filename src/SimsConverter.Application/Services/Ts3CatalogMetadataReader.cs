using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;

namespace SimsConverter.Application.Services;

/// <summary>
/// Extracts real object catalog metadata (price, placement flags, footprint hash, catalog group, identity) from TS3 source packages.
/// </summary>
public class Ts3CatalogMetadataReader : ITs3CatalogMetadataReader
{
    private const uint Ts3ObjdTypeId = 0x319E4F1Du;
    private const uint Ts4ObjdTypeId = 0xC0DB5AE7u;
    private const uint Ts3FtptTypeId = 0x736884F1u;

    private readonly IPackageResourcePayloadReader _payloadReader;

    public Ts3CatalogMetadataReader(IPackageResourcePayloadReader? payloadReader = null)
    {
        _payloadReader = payloadReader ?? new PackageResourcePayloadReader();
    }

    public ObjectCatalogMetadata ReadCatalogMetadata(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "DecorativeObject")
    {
        if (string.IsNullOrEmpty(packagePath) || packageResources == null || packageResources.Count == 0)
        {
            return ObjectCatalogMetadata.CreateDefault(seedIdentity);
        }

        // 1. Locate OBJD catalog resource (TS4 0xC0DB5AE7 preferred if present, otherwise TS3 0x319E4F1D)
        var objdRow = packageResources.FirstOrDefault(r => r.TypeId == Ts4ObjdTypeId)
                   ?? packageResources.FirstOrDefault(r => r.TypeId == Ts3ObjdTypeId);
        var ftptRow = packageResources.FirstOrDefault(r => r.TypeId == Ts3FtptTypeId);

        string? ftptKey = ftptRow?.FormattedKey;
        uint ftptDerivedHash = ftptRow != null ? (uint)(ftptRow.InstanceId & 0xFFFFFFFFUL) : 0u;

        if (objdRow == null)
        {
            var fallback = ObjectCatalogMetadata.CreateDefault(seedIdentity);
            if (ftptRow != null)
            {
                return fallback with
                {
                    FootprintHash = ftptDerivedHash,
                    FootprintReferenceKey = ftptKey
                };
            }
            return fallback;
        }

        // 2. Read OBJD payload
        var entry = new PackageResourceEntry(
            new PackageResourceId(objdRow.TypeId, objdRow.GroupId, objdRow.InstanceId),
            objdRow.Offset,
            objdRow.CompressedSize,
            objdRow.DecompressedSize,
            objdRow.CompressionKind,
            0
        );

        var pRes = _payloadReader.ReadPayload(packagePath, entry);
        if (!pRes.IsSuccess || pRes.Payload == null || pRes.Payload.Length < 16)
        {
            var fallback = ObjectCatalogMetadata.CreateDefault(seedIdentity);
            return ftptRow != null
                ? fallback with { FootprintHash = ftptDerivedHash, FootprintReferenceKey = ftptKey }
                : fallback;
        }

        byte[] payload = pRes.Payload;
        return ParseObjdPayload(payload, objdRow, ftptDerivedHash, ftptKey, seedIdentity);
    }

    public async Task<ObjectCatalogMetadata> ReadCatalogMetadataAsync(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "DecorativeObject",
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ReadCatalogMetadata(packagePath, packageResources, seedIdentity), cancellationToken).ConfigureAwait(false);
    }

    private static ObjectCatalogMetadata ParseObjdPayload(
        byte[] payload,
        PackageResourceRow objdRow,
        uint ftptDerivedHash,
        string? ftptKey,
        string seedIdentity)
    {
        try
        {
            uint price = 100;
            uint catalogGroup = 0;
            uint placementFlags = 0x00000001;
            uint footprintHash = ftptDerivedHash;

            if (objdRow.TypeId == Ts4ObjdTypeId && payload.Length >= 72 && Encoding.ASCII.GetString(payload, 0, 4) == "OBJD")
            {
                placementFlags = BitConverter.ToUInt32(payload, 56);
                footprintHash = BitConverter.ToUInt32(payload, 60);
                price = BitConverter.ToUInt32(payload, 64);
                catalogGroup = BitConverter.ToUInt32(payload, 68);
            }
            else
            {
                int offset = 0;
                if (payload.Length >= 4 && Encoding.ASCII.GetString(payload, 0, 4) == "OBJD")
                {
                    offset = 8; // Header "OBJD" (4) + Version (4)
                }
                else if (payload.Length >= 4)
                {
                    offset = 4; // Version uint32
                }

                if (payload.Length >= offset + 4)
                {
                    price = BitConverter.ToUInt32(payload, offset);
                }

                if (payload.Length >= offset + 8)
                {
                    catalogGroup = BitConverter.ToUInt32(payload, offset + 4);
                }

                if (payload.Length >= offset + 12)
                {
                    placementFlags = BitConverter.ToUInt32(payload, offset + 8);
                    if (placementFlags == 0)
                    {
                        placementFlags = 0x00000001;
                    }
                }

                if (payload.Length >= offset + 16)
                {
                    uint payloadFtptHash = BitConverter.ToUInt32(payload, offset + 12);
                    if (payloadFtptHash != 0)
                    {
                        footprintHash = payloadFtptHash;
                    }
                }
            }

            string identityTitle = !string.IsNullOrWhiteSpace(seedIdentity) && seedIdentity != "DecorativeObject"
                ? seedIdentity
                : $"TS3_Object_{objdRow.InstanceHex}";

            return new ObjectCatalogMetadata(
                Price: price,
                CatalogGroup: catalogGroup,
                PlacementFlags: placementFlags,
                FootprintHash: footprintHash,
                ObjectTitle: identityTitle,
                ObjectIdentityKey: objdRow.FormattedKey,
                IsDefaultFallback: false,
                FootprintReferenceKey: ftptKey
            );
        }
        catch
        {
            var fallback = ObjectCatalogMetadata.CreateDefault(seedIdentity);
            return ftptRowKeyOrHash(fallback, ftptDerivedHash, ftptKey);
        }
    }

    private static ObjectCatalogMetadata ftptRowKeyOrHash(ObjectCatalogMetadata fallback, uint ftptHash, string? ftptKey)
    {
        if (ftptHash != 0 || ftptKey != null)
        {
            return fallback with { FootprintHash = ftptHash != 0 ? ftptHash : fallback.FootprintHash, FootprintReferenceKey = ftptKey };
        }
        return fallback;
    }
}
