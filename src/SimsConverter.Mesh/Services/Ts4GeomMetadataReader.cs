using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Services;

public class Ts4GeomMetadataReader : ITs4GeomMetadataReader
{
    private static readonly byte[] GeomMagicBytes = Encoding.ASCII.GetBytes("GEOM");

    public Ts4GeomParseResult ReadMetadata(byte[] buffer, string? resourceKey = null)
    {
        if (buffer == null)
        {
            return Failure(new ConversionIssue("GEOM000", "Buffer is null.", ConversionIssueSeverity.Error));
        }

        return ReadMetadata(new ReadOnlySpan<byte>(buffer), resourceKey);
    }

    public Ts4GeomParseResult ReadMetadata(ReadOnlySpan<byte> buffer, string? resourceKey = null)
    {
        if (buffer.Length < 20)
        {
            return Failure(new ConversionIssue("GEOM000", "Buffer is shorter than the minimum 20-byte header.", ConversionIssueSeverity.Error));
        }

        int geomChunkOffset = 0;
        uint geomChunkSize = (uint)buffer.Length;
        string containerFormat = "Raw GEOM";

        // Strategy 1: Direct Raw GEOM starting at offset 0
        if (!buffer.Slice(0, 4).SequenceEqual(GeomMagicBytes))
        {
            // Strategy 2: TS4-SimRipper GEOM.cs RCOL Header Alignment
            if (!TryFindRcolGeomChunk(buffer, out geomChunkOffset, out geomChunkSize))
            {
                return Failure(new ConversionIssue("GEOM001", "Magic signature is not 'GEOM' or valid TS4 RCOL container.", ConversionIssueSeverity.Error));
            }
            containerFormat = "RCOL-Wrapped GEOM";
        }

        var geomSpan = buffer.Slice(geomChunkOffset, (int)Math.Min(geomChunkSize, (uint)(buffer.Length - geomChunkOffset)));
        if (geomSpan.Length < 20)
        {
            return Failure(new ConversionIssue("GEOM005", "GEOM payload buffer is truncated.", ConversionIssueSeverity.Error));
        }

        try
        {
            checked
            {
                // 1. Magic
                if (!geomSpan.Slice(0, 4).SequenceEqual(GeomMagicBytes))
                {
                    return Failure(new ConversionIssue("GEOM001", "Magic signature is not 'GEOM'.", ConversionIssueSeverity.Error));
                }

                // 2. Version (TS4 Grounded Versions: 5, 12, 13, 14)
                uint geomVersion = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(4, 4));
                if (geomVersion != 5 && geomVersion != 12 && geomVersion != 13 && geomVersion != 14)
                {
                    return Failure(new ConversionIssue("GEOM002", $"Unsupported GEOM version: {geomVersion}.", ConversionIssueSeverity.Error));
                }

                // 3. Header Offsets & Hashes
                uint rawTgiOffset = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(8, 4));
                uint tgiSize = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(12, 4));
                uint shaderHash = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(16, 4));

                int curr = 20;

                // 4. MTNF Shader Block (if shaderHash != 0)
                uint mtnfSizeBytes = 0;
                if (shaderHash != 0)
                {
                    if (curr + 4 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading MTNF size.", ConversionIssueSeverity.Error));
                    }

                    mtnfSizeBytes = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                    curr += 4;

                    if (curr + (int)mtnfSizeBytes > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading MTNF block.", ConversionIssueSeverity.Error));
                    }

                    curr += (int)mtnfSizeBytes;
                }

                // 5. Merge Group, Sort Order, NumVerts, Fcount
                if (curr + 16 > geomSpan.Length)
                {
                    return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading structural count header.", ConversionIssueSeverity.Error));
                }

                int mergeGroup = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                curr += 4;

                int sortOrder = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                curr += 4;

                uint vertexCount = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                curr += 4;

                uint elementCount = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                curr += 4;

                if (vertexCount == 0)
                {
                    return Failure(new ConversionIssue("GEOM003", "Invalid structural count: VertexCount is zero.", ConversionIssueSeverity.Error));
                }

                // 6. Vertex Descriptors
                uint strideBytes = 0;
                for (int i = 0; i < elementCount; i++)
                {
                    if (curr + 9 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", $"GEOM payload truncated reading element descriptor {i}.", ConversionIssueSeverity.Error));
                    }
                    byte sizeBytes = geomSpan[curr + 8];
                    strideBytes += sizeBytes;
                    curr += 9;
                }

                if (strideBytes == 0)
                {
                    return Failure(new ConversionIssue("GEOM003", "Invalid structural count: VertexStrideBytes is zero.", ConversionIssueSeverity.Error));
                }

                // 7. Vertex Buffer Skip
                int vertexBufferBytes = (int)(vertexCount * strideBytes);
                if (curr + vertexBufferBytes > geomSpan.Length)
                {
                    return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading vertex buffer.", ConversionIssueSeverity.Error));
                }
                curr += vertexBufferBytes;

                // 8. Submesh Face Section
                if (curr + 4 > geomSpan.Length)
                {
                    return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading numSubMeshes.", ConversionIssueSeverity.Error));
                }

                uint numSubMeshes = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                curr += 4;

                uint totalFacePointCount = 0;

                for (int s = 0; s < numSubMeshes; s++)
                {
                    if (curr + 5 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", $"GEOM payload truncated reading submesh {s} header.", ConversionIssueSeverity.Error));
                    }

                    byte bytesPerFacePoint = geomSpan[curr];
                    curr += 1;

                    uint numFacePoints = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                    curr += 4;

                    if (numFacePoints == 0 || numFacePoints % 3 != 0)
                    {
                        return Failure(new ConversionIssue("GEOM003", $"Invalid structural count: Submesh {s} numFacePoints ({numFacePoints}) is zero or not a multiple of 3.", ConversionIssueSeverity.Error));
                    }

                    totalFacePointCount += numFacePoints;

                    int faceBufferBytes = (int)(numFacePoints * (bytesPerFacePoint == 0 ? 2 : bytesPerFacePoint));
                    if (curr + faceBufferBytes > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", $"GEOM payload truncated reading submesh {s} face buffer.", ConversionIssueSeverity.Error));
                    }

                    curr += faceBufferBytes;
                }

                uint faceCount = totalFacePointCount / 3;

                // 9. Version Specific Stitches & Slotrays (TS4-SimRipper GEOM.cs exact byte layout)
                int uvStitchCount = 0;
                int seamStitchCount = 0;
                int slotrayCount = 0;

                if (geomVersion == 5)
                {
                    if (curr + 4 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading v5 skconIndex.", ConversionIssueSeverity.Error));
                    }
                    curr += 4; // Skip skconIndex
                }
                else if (geomVersion >= 12)
                {
                    // UVStitches Loop: uvStitchCount (4B)
                    // For each entry i: vertexIndex (4B) + coordinateCount (4B) + coordinateCount * 8 bytes
                    if (curr + 4 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading uvStitchCount.", ConversionIssueSeverity.Error));
                    }

                    uvStitchCount = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                    curr += 4;

                    if (uvStitchCount < 0)
                    {
                        return Failure(new ConversionIssue("GEOM003", $"Invalid negative UV stitch count: {uvStitchCount}.", ConversionIssueSeverity.Error));
                    }

                    if (uvStitchCount > 0)
                    {
                        for (int i = 0; i < uvStitchCount; i++)
                        {
                            if (curr + 8 > geomSpan.Length)
                            {
                                return Failure(new ConversionIssue("GEOM005", $"GEOM payload truncated reading UV stitch entry {i} header.", ConversionIssueSeverity.Error));
                            }

                            int vertexIndex = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                            curr += 4;

                            int coordCount = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                            curr += 4;

                            if (coordCount < 0)
                            {
                                return Failure(new ConversionIssue("GEOM003", $"Invalid negative UV stitch coordinate count: {coordCount}.", ConversionIssueSeverity.Error));
                            }

                            if (coordCount > 0)
                            {
                                int coordBytes = coordCount * 8;
                                if (curr + coordBytes > geomSpan.Length)
                                {
                                    return Failure(new ConversionIssue("GEOM005", $"GEOM payload truncated reading UV stitch entry {i} coordinates.", ConversionIssueSeverity.Error));
                                }
                                curr += coordBytes;
                            }
                        }
                    }

                    // SeamStitches (v13+): seamStitchCount (4B) + (seamStitchCount > 0 ? seamStitchCount * 6 : 0)
                    if (geomVersion >= 13)
                    {
                        if (curr + 4 > geomSpan.Length)
                        {
                            return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading seamStitchCount.", ConversionIssueSeverity.Error));
                        }

                        seamStitchCount = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                        curr += 4;

                        if (seamStitchCount < 0)
                        {
                            return Failure(new ConversionIssue("GEOM003", $"Invalid negative seam stitch count: {seamStitchCount}.", ConversionIssueSeverity.Error));
                        }

                        if (seamStitchCount > 0)
                        {
                            int seamStitchBytes = seamStitchCount * 6;
                            if (curr + seamStitchBytes > geomSpan.Length)
                            {
                                return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading seam stitch block.", ConversionIssueSeverity.Error));
                            }
                            curr += seamStitchBytes;
                        }
                    }

                    // SlotrayIntersections: slotrayCount (4B) + (slotrayCount > 0 ? slotrayCount * (v14 ? 66 : 63) : 0)
                    if (curr + 4 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading slotrayCount.", ConversionIssueSeverity.Error));
                    }

                    slotrayCount = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                    curr += 4;

                    if (slotrayCount < 0)
                    {
                        return Failure(new ConversionIssue("GEOM003", $"Invalid negative slotray count: {slotrayCount}.", ConversionIssueSeverity.Error));
                    }

                    if (slotrayCount > 0)
                    {
                        int slotrayEntrySize = (geomVersion < 14) ? 63 : 66;
                        int slotrayBytes = slotrayCount * slotrayEntrySize;
                        if (curr + slotrayBytes > geomSpan.Length)
                        {
                            return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading slotray block.", ConversionIssueSeverity.Error));
                        }
                        curr += slotrayBytes;
                    }
                }

                // 10. Bone Section
                if (curr + 4 > geomSpan.Length)
                {
                    return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading bonehashcount.", ConversionIssueSeverity.Error));
                }

                int boneHashCount = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(curr, 4));
                curr += 4;

                if (boneHashCount < 0)
                {
                    return Failure(new ConversionIssue("GEOM003", $"Invalid negative bone hash count: {boneHashCount}.", ConversionIssueSeverity.Error));
                }

                uint boneCount = boneHashCount > 0 ? (uint)boneHashCount : 0u;
                if (boneHashCount > 0)
                {
                    int boneHashBytes = boneHashCount * 4;
                    if (curr + boneHashBytes > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("GEOM005", "GEOM payload truncated reading bone hashes.", ConversionIssueSeverity.Error));
                    }
                    curr += boneHashBytes;
                }

                // 11. Tail TGI Section (read at rawTgiOffset or current location)
                uint embeddedTgiCount = 0;
                int actualTgiPos = (int)(rawTgiOffset + 12);
                if (actualTgiPos >= 0 && actualTgiPos + 4 <= geomSpan.Length)
                {
                    int numTgi = BinaryPrimitives.ReadInt32LittleEndian(geomSpan.Slice(actualTgiPos, 4));
                    if (numTgi > 0 && actualTgiPos + 4 + (numTgi * 16) <= geomSpan.Length)
                    {
                        embeddedTgiCount = (uint)numTgi;
                    }
                }

                var metadata = new Ts4GeomMetadata(
                    ContainerFormat: containerFormat,
                    GeomVersion: geomVersion,
                    ShaderHash: shaderHash,
                    MtnfSizeBytes: mtnfSizeBytes,
                    MergeGroup: mergeGroup,
                    SortOrder: sortOrder,
                    VertexCount: vertexCount,
                    FaceCount: faceCount,
                    FacePointCount: totalFacePointCount,
                    VertexStrideBytes: strideBytes,
                    VertexElementCount: elementCount,
                    NumSubMeshes: numSubMeshes,
                    UvStitchCount: uvStitchCount,
                    SeamStitchCount: seamStitchCount,
                    SlotrayCount: slotrayCount,
                    BoneCount: boneCount,
                    EmbeddedTgiCount: embeddedTgiCount,
                    TotalSizeBytes: buffer.Length,
                    GeomChunkOffset: geomChunkOffset,
                    GeomChunkSize: geomChunkSize,
                    TgiOffset: rawTgiOffset,
                    TgiSize: tgiSize
                );

                return new Ts4GeomParseResult(true, metadata, Array.Empty<ConversionIssue>());
            }
        }
        catch (OverflowException)
        {
            return Failure(new ConversionIssue("GEOM004", "Arithmetic overflow detected while evaluating offsets or section lengths.", ConversionIssueSeverity.Error));
        }
        catch (Exception ex)
        {
            return Failure(new ConversionIssue("GEOM005", $"Failed to parse TS4 GEOM structural metadata: {ex.Message}", ConversionIssueSeverity.Error));
        }
    }

    private static bool TryFindRcolGeomChunk(ReadOnlySpan<byte> buffer, out int geomChunkOffset, out uint geomChunkSize)
    {
        geomChunkOffset = -1;
        geomChunkSize = 0;

        // TS4-SimRipper RCOL Header alignment:
        // version1 (4B), count (4B), ind3 (4B), extCount (4B), intCount (4B), dummyTGI (16B), abspos (4B), meshsize (4B)
        if (buffer.Length < 44)
        {
            return false;
        }

        try
        {
            checked
            {
                // Strategy A: Count-First RCOL Header (offset 0: extCount, offset 4: intCount)
                uint cfExtCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4));
                uint cfIntCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4));

                if (cfIntCount > 0 && cfIntCount <= 100 && cfExtCount <= 100)
                {
                    int locTableStart = 8 + (int)(cfIntCount * 16) + (int)(cfExtCount * 16);
                    int locTableEnd = locTableStart + (int)(cfIntCount * 8);

                    if (locTableEnd <= buffer.Length)
                    {
                        for (int i = 0; i < (int)cfIntCount; i++)
                        {
                            int locPos = locTableStart + (i * 8);
                            uint pos = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(locPos, 4));
                            uint size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(locPos + 4, 4));

                            int targetPos = 8 + (int)pos;
                            if (targetPos >= 8 && targetPos + size <= (uint)buffer.Length && size >= 20)
                            {
                                if (buffer.Slice(targetPos, 4).SequenceEqual(GeomMagicBytes))
                                {
                                    geomChunkOffset = targetPos;
                                    geomChunkSize = size;
                                    return true;
                                }
                            }
                        }
                    }
                }

                // Strategy B: TS4-SimRipper RCOL Header alignment
                uint version1 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4));
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4));
                uint ind3 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4));
                uint extCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(12, 4));
                uint intCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(16, 4));

                // TS4-SimRipper GEOM.ReadFile sequence:
                // reads dummyTGI (16B), then abspos (4B) and meshsize (4B)
                uint abspos = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(36, 4));
                uint meshsize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(40, 4));

                if (abspos >= 44 && abspos + meshsize <= (uint)buffer.Length && meshsize >= 20)
                {
                    if (buffer.Slice((int)abspos, 4).SequenceEqual(GeomMagicBytes))
                    {
                        geomChunkOffset = (int)abspos;
                        geomChunkSize = meshsize;
                        return true;
                    }
                }

                // Fallback: Scan internal chunk locations table if intCount > 1
                if (intCount > 0 && intCount <= 100 && extCount <= 100)
                {
                    int curr = 20 + (int)(extCount * 16);
                    int locTableEnd = curr + (int)(intCount * 8);

                    if (locTableEnd <= buffer.Length)
                    {
                        for (int i = 0; i < intCount; i++)
                        {
                            int locPos = curr + (i * 8);
                            uint pos = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(locPos, 4));
                            uint size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(locPos + 4, 4));

                            if (pos >= 20 && pos + size <= (uint)buffer.Length && size >= 20)
                            {
                                if (buffer.Slice((int)pos, 4).SequenceEqual(GeomMagicBytes))
                                {
                                    geomChunkOffset = (int)pos;
                                    geomChunkSize = size;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static Ts4GeomParseResult Failure(ConversionIssue issue)
    {
        return new Ts4GeomParseResult(false, null, new[] { issue });
    }
}
