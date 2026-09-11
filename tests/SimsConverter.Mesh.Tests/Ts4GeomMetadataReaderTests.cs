using System;
using System.Buffers.Binary;
using System.Text;
using SimsConverter.Mesh.Models;
using SimsConverter.Mesh.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Mesh.Tests;

public class Ts4GeomMetadataReaderTests
{
    private readonly Ts4GeomMetadataReader _reader = new();

    private static byte[] CreateValidTs4GeomPayload(
        uint geomVersion = 12,
        int vertexCount = 3,
        int facePointCount = 3,
        int boneCount = 1,
        int uvStitchCount = 0,
        int[]? uvStitchCoordCounts = null,
        int seamStitchCount = 0,
        int slotrayCount = 0)
    {
        int elementCount = 3;
        int strideBytes = 32;

        int vertexBufferBytes = vertexCount * strideBytes;
        int indexBufferBytes = facePointCount * 2;
        int boneHashBytes = boneCount * 4;
        int submeshBytes = 4 + 1 + 4 + indexBufferBytes; // numSubMeshes (4B) + faceFormat (1B) + numfacepoints (4B) + indices

        int stitchesBytes = 0;
        if (geomVersion == 5)
        {
            stitchesBytes = 4; // skconIndex
        }
        else if (geomVersion >= 12)
        {
            // UVStitches Loop: uvStitchCount (4B) + sum(8 + coordCount * 8)
            stitchesBytes += 4;
            if (uvStitchCount > 0 && uvStitchCoordCounts != null)
            {
                foreach (var cCount in uvStitchCoordCounts)
                {
                    stitchesBytes += 8 + (cCount * 8);
                }
            }

            // SeamStitches (v13+): seamStitchCount (4B) + (seamStitchCount > 0 ? seamStitchCount * 6 : 0)
            if (geomVersion >= 13)
            {
                stitchesBytes += 4;
                if (seamStitchCount > 0)
                {
                    stitchesBytes += seamStitchCount * 6;
                }
            }

            // SlotrayIntersections: slotrayCount (4B) + (slotrayCount > 0 ? slotrayCount * (v14 ? 66 : 63) : 0)
            stitchesBytes += 4;
            if (slotrayCount > 0)
            {
                int entrySize = (geomVersion < 14) ? 63 : 66;
                stitchesBytes += slotrayCount * entrySize;
            }
        }

        int boneSectionBytes = 4 + boneHashBytes;
        int tailTgiBytes = 4 + 16; // numtgi = 1, TGI entry 16B

        int geomChunkLength = 20 + 16 + (elementCount * 9) + vertexBufferBytes + submeshBytes + stitchesBytes + boneSectionBytes + tailTgiBytes;
        int tailStartPos = geomChunkLength - tailTgiBytes;
        uint rawTgiOffset = (uint)(tailStartPos - 12);
        uint tgiSize = (uint)tailTgiBytes;

        var buffer = new byte[geomChunkLength];
        var span = buffer.AsSpan();

        // 1. Magic
        Encoding.ASCII.GetBytes("GEOM", span.Slice(0, 4));
        // 2. Version
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), geomVersion);
        // 3. TGI Offset
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), rawTgiOffset);
        // 4. TGI Size
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), tgiSize);
        // 5. Shader Hash (0 = no MTNF block)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0);

        int curr = 20;

        // Merge Group & Sort Order
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0);
        curr += 4;

        // NumVerts & Fcount
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), (uint)vertexCount);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), (uint)elementCount);
        curr += 4;

        // Element Descriptors (3 elements)
        // E0: Position float3 (12B)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 4, 4), 3);
        span[curr + 8] = 12;
        curr += 9;

        // E1: Normal float3 (12B)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 4, 4), 3);
        span[curr + 8] = 12;
        curr += 9;

        // E2: UV0 float2 (8B)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 4, 4), 2);
        span[curr + 8] = 8;
        curr += 9;

        // Vertex buffer
        curr += vertexBufferBytes;

        // Submesh Section
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 1); // numSubMeshes = 1
        curr += 4;
        span[curr] = 0x02; // bytesperfacepnt = 2 (UInt16)
        curr += 1;
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), (uint)facePointCount);
        curr += 4;

        // Indices
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(curr, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(curr + 2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(curr + 4, 2), 2);
        curr += indexBufferBytes;

        // Stitches & Slotrays
        if (geomVersion == 5)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0); // skconIndex
            curr += 4;
        }
        else if (geomVersion >= 12)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), uvStitchCount);
            curr += 4;
            if (uvStitchCount > 0 && uvStitchCoordCounts != null)
            {
                for (int i = 0; i < uvStitchCount; i++)
                {
                    int vIndex = (i + 1) * 10;
                    int cCount = uvStitchCoordCounts[i];

                    BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), vIndex);
                    curr += 4;
                    BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), cCount);
                    curr += 4;

                    if (cCount > 0)
                    {
                        curr += cCount * 8;
                    }
                }
            }

            if (geomVersion >= 13)
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), seamStitchCount);
                curr += 4;
                if (seamStitchCount > 0)
                {
                    curr += seamStitchCount * 6;
                }
            }

            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), slotrayCount);
            curr += 4;
            if (slotrayCount > 0)
            {
                int entrySize = (geomVersion < 14) ? 63 : 66;
                curr += slotrayCount * entrySize;
            }
        }

        // Bone Section
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), boneCount);
        curr += 4;

        // Bone Hashes
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 0xAAAA0000);
        curr += boneHashBytes;

        // Tail TGI List (numtgi = 1, TGI entry = 16B)
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(curr + 4, 8), 0x9999888877776666UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 12, 4), 0x00B2D882);

        return buffer;
    }

    private static byte[] CreateValidTs4RcolGeomContainer(uint geomVersion = 12)
    {
        var geomChunk = CreateValidTs4GeomPayload(geomVersion: geomVersion);
        int rcolHeaderBytes = 20; // version1, count, ind3, extCount, intCount
        int dummyTgiBytes = 16;   // dummyTGI (16B)
        int locBytes = 8;        // abspos (4B), meshsize (4B)
        int geomChunkOffset = rcolHeaderBytes + dummyTgiBytes + locBytes;
        int totalBytes = geomChunkOffset + geomChunk.Length;

        var buffer = new byte[totalBytes];
        var span = buffer.AsSpan();

        // RCOL Header
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1); // version1
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1); // count
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), 0); // ind3
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), 1); // extCount
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 1); // intCount

        // Dummy TGI (16B)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(20, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(28, 4), 0x00B2D882);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x00000000);

        // Location Table (abspos 4B, meshsize 4B)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), (uint)geomChunkOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)geomChunk.Length);

        // Copy GEOM chunk payload
        geomChunk.CopyTo(buffer, geomChunkOffset);

        return buffer;
    }

    [Fact]
    public void ReadMetadata_ValidTs4GeomV12Rcol_ReturnsSuccessAndMetadata()
    {
        // Arrange: TS4 RCOL container wrapping Version 12 GEOM
        var buffer = CreateValidTs4RcolGeomContainer(geomVersion: 12);

        // Act
        var result = _reader.ReadMetadata(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.Metadata.Should().NotBeNull();

        var meta = result.Metadata!;
        meta.ContainerFormat.Should().Be("RCOL-Wrapped GEOM");
        meta.GeomVersion.Should().Be(12);
        meta.VertexCount.Should().Be(3);
        meta.FaceCount.Should().Be(1);
        meta.FacePointCount.Should().Be(3);
        meta.VertexStrideBytes.Should().Be(32);
        meta.BoneCount.Should().Be(1);
        meta.EmbeddedTgiCount.Should().Be(1);
        meta.GeomChunkOffset.Should().Be(44);
    }

    [Theory]
    [InlineData(13u)]
    [InlineData(14u)]
    public void ReadMetadata_ValidTs4GeomV13OrV14_ReturnsSuccessAndMetadata(uint version)
    {
        // Arrange: TS4 GEOM version 13 or 14 payload
        var buffer = CreateValidTs4GeomPayload(geomVersion: version);

        // Act
        var result = _reader.ReadMetadata(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!.GeomVersion.Should().Be(version);
        result.Metadata.EmbeddedTgiCount.Should().Be(1);
    }

    [Fact]
    public void ReadMetadata_ValidTs4GeomV13WithMultipleUvStitchesDifferentCoordinateCounts_ParsesMetadataAndReadsTailTgi()
    {
        // Arrange: TS4 GEOM v13 with 2 UV stitches of different coordinate counts (entry 0 count=0 -> 8B, entry 1 count=2 -> 24B, total 32B)
        var buffer = CreateValidTs4GeomPayload(
            geomVersion: 13,
            vertexCount: 3,
            facePointCount: 3,
            boneCount: 1,
            uvStitchCount: 2,
            uvStitchCoordCounts: new[] { 0, 2 },
            seamStitchCount: 1,
            slotrayCount: 0);

        // Act
        var result = _reader.ReadMetadata(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();

        var meta = result.Metadata!;
        meta.GeomVersion.Should().Be(13);
        meta.UvStitchCount.Should().Be(2);
        meta.SeamStitchCount.Should().Be(1);
        meta.BoneCount.Should().Be(1);
        meta.EmbeddedTgiCount.Should().Be(1, "Precise loop MUST process entry 0 (8B) and entry 1 (24B) to advance cursor 32B for UVStitches, then 6B for SeamStitch to read bone section and tail TGI");
    }

    [Fact]
    public void ReadMetadata_ValidTs4GeomV14WithNonZeroSlotrayBlock_ParsesMetadataAndReadsTailTgi()
    {
        // Arrange: TS4 GEOM v14 with non-zero slotray count (1) (66 bytes entry)
        var buffer = CreateValidTs4GeomPayload(
            geomVersion: 14,
            vertexCount: 3,
            facePointCount: 3,
            boneCount: 1,
            uvStitchCount: 0,
            uvStitchCoordCounts: null,
            seamStitchCount: 0,
            slotrayCount: 1);

        // Act
        var result = _reader.ReadMetadata(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();

        var meta = result.Metadata!;
        meta.GeomVersion.Should().Be(14);
        meta.SlotrayCount.Should().Be(1);
        meta.BoneCount.Should().Be(1);
        meta.EmbeddedTgiCount.Should().Be(1, "Cursor MUST skip v14 66-byte slotray entry to read bone section and tail TGI");
    }

    [Fact]
    public void ReadMetadata_UnsupportedVersion_ReturnsControlledError()
    {
        // Arrange: Version 99
        var buffer = CreateValidTs4GeomPayload(geomVersion: 99);

        // Act
        var result = _reader.ReadMetadata(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("GEOM002");
    }

    [Fact]
    public void ReadMetadata_TruncatedRcolHeader_ReturnsControlledErrorWithoutCrashing()
    {
        // Arrange: Only 8 bytes
        var truncated = new byte[8];

        // Act
        var result = _reader.ReadMetadata(truncated);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("GEOM000");
    }

    [Fact]
    public void ReadMetadata_DoesNotMutateSourceBuffer()
    {
        // Arrange
        var original = CreateValidTs4RcolGeomContainer();
        var clone = (byte[])original.Clone();

        // Act
        _reader.ReadMetadata(original);

        // Assert
        original.Should().Equal(clone, "ReadMetadata MUST remain read-only and MUST NOT mutate the source buffer.");
    }
}
