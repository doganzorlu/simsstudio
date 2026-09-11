using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Mesh.Tests;

public class Ts3GeomMetadataReaderTests
{
    private readonly Ts3GeomMetadataReader _reader = new();

    private static byte[] CreateValidReferenceCountFirstRcolGeomBuffer(
        uint geomVersion = 5,
        bool includeMtnf = false,
        uint vertexCount = 3,
        uint facePointCount = 3,
        uint boneCount = 1,
        uint overrideTgiOffset = 0,
        uint overrideTgiSize = 0,
        bool omitTgiTail = false)
    {
        // 1. Primary Count-First RCOL Header (geom_write.py default):
        // ExternalTgiCount (4B at offset 0) + InternalResourceCount (4B at offset 4) = 8B headerSize
        uint headerSize = 8;

        // 2. Internal ITG Array: 1 * 16B = 16B (offset 8)
        // 3. External TGI Array: 1 * 16B = 16B (offset 24)
        // 4. Chunk Location Table: 1 * 8B = 8B (offset 40)
        // Total RCOL Header & Index = 48 bytes
        int geomChunkOffset = 48;

        int mtnfSizeBytes = includeMtnf ? 16 : 4; // 4B zero padding if no MTNF (geom_write.py line 70)
        int vertexStrideBytes = 20;
        int vertexBufferBytes = (int)vertexCount * vertexStrideBytes;
        int indexBufferBytes = (int)facePointCount * 2;
        int boneHashBytes = (int)boneCount * 4;

        // Face Group Header (geom_write.py lines 100-120): groupMarker (4B) + faceFormat (1B) + facePointCount (4B) = 9B
        int faceGroupHeaderBytes = 9;

        // Embedded TGI Tail (geom_write.py lines 62 & 143): 4B count + 16B TGI entry = 20B
        int embeddedTgiSizeBytes = omitTgiTail ? 0 : 20;
        int geomChunkLength = 20 + mtnfSizeBytes + 16 + 18 + vertexBufferBytes + faceGroupHeaderBytes + indexBufferBytes + 8 + boneHashBytes + embeddedTgiSizeBytes;

        int tailStartPos = geomChunkLength - embeddedTgiSizeBytes;

        // Reference geom_write.py line 62: rawTgiOffset = tailStart - 12
        uint calculatedRawTgiOffset = omitTgiTail ? 0 : (uint)(tailStartPos - 12);
        uint calculatedTgiSize = omitTgiTail ? 0 : (uint)embeddedTgiSizeBytes;

        if (overrideTgiOffset > 0) calculatedRawTgiOffset = overrideTgiOffset;
        if (overrideTgiSize > 0) calculatedTgiSize = overrideTgiSize;

        int totalSizeBytes = geomChunkOffset + geomChunkLength;

        var buffer = new byte[totalSizeBytes];
        var span = buffer.AsSpan();

        // 1. Count-First RCOL Header
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1); // ExternalTgiCount = 1
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1); // InternalResourceCount = 1

        // 2. Internal ITG (16 bytes: InstanceId 8B, TypeId 4B, GroupId 4B) at offset 8
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), 0x5555666677778888UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x015A1849); // GEOM TypeId at offset 16
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0x00000000);

        // 3. External TGI (16 bytes) at offset 24
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x00B2D882);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), 0x00000000);

        // 4. Chunk Location Table (8 bytes: Position uint32, Size uint32) at offset 40
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)(geomChunkOffset - (int)headerSize));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), (uint)geomChunkLength);

        // 5. GEOM Chunk at offset 48
        var geomSpan = span.Slice(geomChunkOffset);
        Encoding.ASCII.GetBytes("GEOM", geomSpan.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(4, 4), geomVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(8, 4), calculatedRawTgiOffset); // TgiOffset (tailStart - 12)
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(12, 4), calculatedTgiSize);      // TgiSize
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0x00000000);            // EmbeddedId = 0

        int curr = 20;

        if (includeMtnf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 8); // MtnfSize = 8
            Encoding.ASCII.GetBytes("MTNF", geomSpan.Slice(curr + 4, 4));
            curr += 16;
        }
        else
        {
            // geom_write.py line 70: write 4 bytes zero padding when EmbeddedId == 0 and MTNF omitted
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
            curr += 4;
        }

        // MergeGroup (4B), SortOrder (4B), VertexCount (4B), VertexElementCount (4B)
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), vertexCount);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 2); // VertexElementCount = 2
        curr += 4;

        // Reference geom_write.py Descriptor 1 (9 bytes): DataType (4B = 1 [Position]), Format (4B = 3 [Float3]), SizeBytes (1B = 12)
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 3);
        geomSpan[curr + 8] = 12;
        curr += 9;

        // Reference geom_write.py Descriptor 2 (9 bytes): DataType (4B = 2 [UV0]), Format (4B = 2 [Float2]), SizeBytes (1B = 8)
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 2);
        geomSpan[curr + 8] = 8;
        curr += 9;

        // Skip Vertex Data
        curr += vertexBufferBytes;

        // Reference geom_write.py lines 100-120: groupMarker (4B) + faceFormat (1B) + facePointCount (4B)
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1); // groupMarker = 1
        curr += 4;
        geomSpan[curr] = 0x02; // faceFormat = 2
        curr += 1;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), facePointCount);
        curr += 4;

        // Index Buffer (facePointCount * 2B)
        curr += indexBufferBytes;

        // SkinControllerIndex (4B), BoneCount (4B)
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), boneCount);
        curr += 4;

        // Bone Hashes (boneCount * 4B)
        for (int i = 0; i < boneCount; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + i * 4, 4), (uint)(0xAAAA0000 + i));
        }
        curr += boneHashBytes;

        // Embedded TGI Tail (geom_write.py): tgiCount (4B) + 16B TGI entry
        if (!omitTgiTail)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1); // tgiCount = 1
            BinaryPrimitives.WriteUInt64LittleEndian(geomSpan.Slice(curr + 4, 8), 0x9999888877776666UL);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), 0x00B2D882);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 16, 4), 0x00000000);
        }

        return buffer;
    }

    [Fact]
    public void Read_ValidReferenceCountFirstRcolGeomLayout_ParsesMetadataAndTgiTailSuccessfully()
    {
        // Arrange: Mandatory count-first RCOL layout with exact geom_write.py TGI tail
        var buffer = CreateValidReferenceCountFirstRcolGeomBuffer(geomVersion: 5, includeMtnf: false);

        // Act
        var result = _reader.Read(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!.ContainerFormat.Should().Be("CountFirstRCOL");
        result.Metadata.GeomVersion.Should().Be(5);
        result.Metadata.VertexCount.Should().Be(3);
        result.Metadata.IndexCount.Should().Be(3);
        result.Metadata.FaceCount.Should().Be(1);
        result.Metadata.VertexStrideBytes.Should().Be(20);
        result.Metadata.BoneCount.Should().Be(1);
    }

    [Fact]
    public void Read_LegacyFaceGroupHeaderStructure_IsRejected()
    {
        // Arrange: Create a buffer using old fake "faceGroupCount (4B) + 5B header" layout
        // Old fake layout wrote: faceGroupCount (4B) + 5B header (1B + 4B zero) + facePointCount (4B)
        var buffer = CreateValidReferenceCountFirstRcolGeomBuffer(omitTgiTail: true);

        // Mutate face group section (at buffer offset 48 + 118 = 166) to old fake "faceGroupCount" pattern
        // Replace groupMarker (4B) + faceFormat (1B) + facePointCount (4B) with fake faceGroupCount (4B = 1) + 5B header (1B + 4B = 0)
        int fgOffset = 48 + 118;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(fgOffset, 4), 1); // faceGroupCount = 1
        buffer[fgOffset + 4] = 0x01;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(fgOffset + 5, 4), 0); // 4 zeros read as indexCount -> indexCount == 0

        // Act
        var result = _reader.Read(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse("Legacy fake 'faceGroupCount + 5B header' MUST be rejected");
        result.Issues.Should().Contain(i => i.Code == "GEOM003" || i.Code == "GEOM005");
    }

    [Fact]
    public void Read_RawGeomChunkWithoutTgiTail_ParsesMetadataSuccessfully()
    {
        // Arrange: Legacy/raw GEOM chunk with tgiSize == 0
        var buffer = CreateValidReferenceCountFirstRcolGeomBuffer(omitTgiTail: true);

        // Act
        var result = _reader.Read(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!.VertexCount.Should().Be(3);
    }

    [Fact]
    public void Read_MtnfSizeOverflow_FailsWithGEOM004OrGEOM005()
    {
        // Arrange: GEOM payload with corrupted MTNF size (0xFFFFFFFF)
        var buffer = CreateValidReferenceCountFirstRcolGeomBuffer(includeMtnf: true);
        // MTNF size is at geomChunkOffset (48) + 20 = offset 68
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(68, 4), 0xFFFFFFFF);

        // Act
        var result = _reader.Read(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "GEOM004" || i.Code == "GEOM005");
    }

    [Fact]
    public void Read_EmbeddedTgiSizeMismatch_FailsWithGEOM005()
    {
        // Arrange: GEOM header with customTgiSize != 4 + count * 16
        var buffer = CreateValidReferenceCountFirstRcolGeomBuffer(overrideTgiOffset: 20, overrideTgiSize: 99);

        // Act
        var result = _reader.Read(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "GEOM005");
    }

    [Fact]
    public void Read_MalformedTgiOffsetOrSize_FailsWithGEOM005()
    {
        // Arrange: GEOM header with customTgiOffset past chunk end
        var buffer = CreateValidReferenceCountFirstRcolGeomBuffer(overrideTgiOffset: 5000, overrideTgiSize: 100);

        // Act
        var result = _reader.Read(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "GEOM005");
    }

    [Fact]
    public void Read_TruncatedBoneHashList_FailsWithGEOM005()
    {
        // Arrange: Buffer truncated before completing bone hash list
        var fullBuffer = CreateValidReferenceCountFirstRcolGeomBuffer(boneCount: 10, omitTgiTail: true);
        int truncatedLength = fullBuffer.Length - 20; // Cut off bone hash payload
        var truncatedBuffer = new byte[truncatedLength];
        Array.Copy(fullBuffer, truncatedBuffer, truncatedLength);

        // Update Chunk Location Table Size (offset 44) to match truncated chunk size
        BinaryPrimitives.WriteUInt32LittleEndian(truncatedBuffer.AsSpan(44, 4), (uint)(truncatedLength - 48));

        // Act
        var result = _reader.Read(truncatedBuffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "GEOM005");
    }

    [Fact]
    public void Read_FacePointCountNotDivisibleByThree_FailsWithGEOM003()
    {
        // Arrange: FacePointCount = 4 (not divisible by 3)
        var buffer = CreateValidReferenceCountFirstRcolGeomBuffer(facePointCount: 4);

        // Act
        var result = _reader.Read(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "GEOM003");
    }

    [Fact]
    public void Read_NullOrEmptyBuffer_FailsWithGEOM000()
    {
        // Act
        var result = _reader.Read(ReadOnlySpan<byte>.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "GEOM000");
    }

    [Fact]
    public void Read_BufferTooShort_FailsWithGEOM000()
    {
        // Arrange: Buffer shorter than 16 bytes
        var shortBuffer = new byte[10];

        // Act
        var result = _reader.Read(shortBuffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "GEOM000");
    }

    [Fact]
    public async Task ReadAsync_StreamInvariantsPreserved_DoesNotAlterStreamPositionOnError()
    {
        // Arrange: Stream with invalid signature at position 10
        var invalidBuffer = new byte[50];
        using var stream = new MemoryStream(invalidBuffer);
        stream.Position = 10;

        // Act
        var result = await _reader.ReadAsync(stream);

        // Assert
        result.IsSuccess.Should().BeFalse();
        stream.Position.Should().Be(10, "Stream position MUST be preserved on error");
    }
}
