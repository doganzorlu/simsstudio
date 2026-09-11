using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Services;

public class Ts3ObjectModelMetadataReader : ITs3ObjectModelMetadataReader
{
    private const uint ModlTypeId = 0x01661233u;
    private const uint MlodTypeId = 0x01D10F34u;
    private const uint GeomTypeId = 0x015A1849u;

    public Ts3ObjectModelMetadataResult Read(ReadOnlySpan<byte> buffer, PackageResourceId resourceId)
    {
        if (resourceId == null)
        {
            return Ts3ObjectModelMetadataResult.Failure(
                new PackageResourceId(0, 0, 0),
                Ts3ObjectModelKind.Unknown,
                "MODL000",
                "ResourceId is null."
            );
        }

        var issues = new List<ConversionIssue>();

        if (buffer.IsEmpty || buffer.Length < 4)
        {
            return Ts3ObjectModelMetadataResult.Failure(
                resourceId,
                ClassifyKind(resourceId.TypeId),
                "MODL001",
                $"Payload buffer too small for MODL/MLOD metadata decoding (actual: {buffer.Length} bytes)."
            );
        }

        var modelKind = ClassifyKind(resourceId.TypeId);
        if (modelKind == Ts3ObjectModelKind.Unknown)
        {
            return Ts3ObjectModelMetadataResult.Failure(
                resourceId,
                Ts3ObjectModelKind.Unknown,
                "MODL002",
                $"Unrecognized TypeId 0x{resourceId.TypeId:X8} for TS3 Object Model metadata reader."
            );
        }

        if (buffer.Length >= 2 && (IsRefpackHeader(buffer) || buffer[0] == 0x78))
        {
            return Ts3ObjectModelMetadataResult.Failure(
                resourceId,
                modelKind,
                "MODL004",
                "Compressed payload detected; decompressed payload required for MODL/MLOD metadata reader."
            );
        }

        try
        {
            uint rawVersion = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4));
            var lodInfos = new List<Ts3ObjectModelLodInfo>();
            var geomRefs = new List<Ts3ObjectModelGeometryReference>();

            // Heuristic TGI resource reference scanning (16-byte TGI blocks: TypeId 4B, GroupId 4B, InstanceId 8B)
            for (int pos = 0; pos <= buffer.Length - 16; pos += 4)
            {
                uint candidateTypeId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(pos, 4));
                if (candidateTypeId == GeomTypeId)
                {
                    uint candidateGroupId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(pos + 4, 4));
                    ulong candidateInstanceId = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(pos + 8, 8));
                    var targetId = new PackageResourceId(candidateTypeId, candidateGroupId, candidateInstanceId);
                    geomRefs.Add(new Ts3ObjectModelGeometryReference(targetId, (uint)geomRefs.Count, "HeuristicGeometryCandidate"));
                }
                else if (candidateTypeId == MlodTypeId && modelKind == Ts3ObjectModelKind.Modl)
                {
                    uint candidateGroupId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(pos + 4, 4));
                    ulong candidateInstanceId = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(pos + 8, 8));
                    var targetId = new PackageResourceId(candidateTypeId, candidateGroupId, candidateInstanceId);
                    uint lodIdx = (uint)lodInfos.Count;
                    lodInfos.Add(new Ts3ObjectModelLodInfo(lodIdx, 1, targetId));
                }
            }

            // Default LOD info if none found explicitly
            if (lodInfos.Count == 0 && modelKind == Ts3ObjectModelKind.Mlod)
            {
                lodInfos.Add(new Ts3ObjectModelLodInfo(0, 1, resourceId));
            }

            uint lodCount = (uint)lodInfos.Count;

            return new Ts3ObjectModelMetadataResult(
                IsSuccess: true,
                ResourceId: resourceId,
                ModelKind: modelKind,
                Version: rawVersion,
                LodCount: lodCount,
                LodInfos: lodInfos.AsReadOnly(),
                GeometryReferences: geomRefs.AsReadOnly(),
                Issues: issues.AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            return Ts3ObjectModelMetadataResult.Failure(
                resourceId,
                modelKind,
                "MODL003",
                $"Exception while parsing MODL/MLOD payload: {ex.Message}"
            );
        }
    }

    public Ts3ObjectModelMetadataResult Read(Stream stream, PackageResourceId resourceId)
    {
        if (stream == null)
        {
            return Ts3ObjectModelMetadataResult.Failure(
                resourceId ?? new PackageResourceId(0, 0, 0),
                Ts3ObjectModelKind.Unknown,
                "MODL000",
                "Stream is null."
            );
        }

        long initialPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return Read(memoryStream.ToArray(), resourceId);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = initialPosition;
            }
        }
    }

    public async Task<Ts3ObjectModelMetadataResult> ReadAsync(
        Stream stream,
        PackageResourceId resourceId,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            return Ts3ObjectModelMetadataResult.Failure(
                resourceId ?? new PackageResourceId(0, 0, 0),
                Ts3ObjectModelKind.Unknown,
                "MODL000",
                "Stream is null."
            );
        }

        long initialPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            return Read(memoryStream.ToArray(), resourceId);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = initialPosition;
            }
        }
    }

    private static bool IsRefpackHeader(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty || buffer.Length < 2) return false;
        byte b0 = buffer[0];
        byte b1 = buffer[1];
        return (b1 == 0xFB && (b0 & 0x1F) == 0x10) || (b0 == 0xFB && (b1 & 0x1F) == 0x10);
    }

    private static Ts3ObjectModelKind ClassifyKind(uint typeId) => typeId switch
    {
        ModlTypeId => Ts3ObjectModelKind.Modl,
        MlodTypeId => Ts3ObjectModelKind.Mlod,
        _ => Ts3ObjectModelKind.Unknown
    };
}
