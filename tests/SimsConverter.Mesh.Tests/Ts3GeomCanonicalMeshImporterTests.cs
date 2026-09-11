using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Mesh.Tests;

public class Ts3GeomCanonicalMeshImporterTests
{
    private readonly Ts3GeomCanonicalMeshImporter _importer = new();

    private static byte[] CreateValidCountFirstGeomBuffer(
        uint geomVersion = 5,
        bool includeNormal = true,
        bool includeUv = true,
        bool includeBoneWeights = false,
        uint unknownDatatype = 0,
        ushort customIndex = 0xFFFF)
    {
        uint headerSize = 8;
        int geomChunkOffset = 48;

        int vertexCount = 3;
        int facePointCount = 3;
        int boneCount = 1;

        int elementCount = 1;
        int strideBytes = 12;

        if (includeNormal) { elementCount++; strideBytes += 12; }
        if (includeUv) { elementCount++; strideBytes += 8; }
        if (includeBoneWeights) { elementCount += 2; strideBytes += 4 + 16; }
        if (unknownDatatype > 0) { elementCount++; strideBytes += 4; }

        int vertexBufferBytes = vertexCount * strideBytes;
        int indexBufferBytes = facePointCount * 2;
        int boneHashBytes = boneCount * 4;
        int faceGroupHeaderBytes = 9; // groupMarker 4B + faceFormat 1B + facePointCount 4B
        int embeddedTgiSizeBytes = 20;

        int geomChunkLength = 20 + 4 + 16 + (elementCount * 9) + vertexBufferBytes + faceGroupHeaderBytes + indexBufferBytes + 8 + boneHashBytes + embeddedTgiSizeBytes;
        int tailStartPos = geomChunkLength - embeddedTgiSizeBytes;

        uint rawTgiOffset = (uint)(tailStartPos - 12);
        uint tgiSize = (uint)embeddedTgiSizeBytes;

        int totalSizeBytes = geomChunkOffset + geomChunkLength;
        var buffer = new byte[totalSizeBytes];
        var span = buffer.AsSpan();

        // 1. Count-First RCOL Header
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1); // ExternalTgiCount = 1
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1); // InternalResourceCount = 1

        // 2. Internal ITG at offset 8
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), 0x5555666677778888UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x015A1849); // GEOM TypeId
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0x00000000);

        // 3. External TGI at offset 24
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x00B2D882);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), 0x00000000);

        // 4. Chunk Location Table at offset 40
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)(geomChunkOffset - (int)headerSize));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), (uint)geomChunkLength);

        // 5. GEOM Chunk at offset 48
        var geomSpan = span.Slice(geomChunkOffset);
        Encoding.ASCII.GetBytes("GEOM", geomSpan.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(4, 4), geomVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(8, 4), rawTgiOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(12, 4), tgiSize);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0x00000000);

        int curr = 20;
        // Zero padding for EmbeddedId == 0
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;

        // MergeGroup (4B), SortOrder (4B), VertexCount (4B), VertexElementCount (4B)
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)vertexCount);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)elementCount);
        curr += 4;

        // Descriptor 1: Position
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 3);
        geomSpan[curr + 8] = 12;
        curr += 9;

        if (includeNormal)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 3);
            geomSpan[curr + 8] = 12;
            curr += 9;
        }

        if (includeUv)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 3);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 2);
            geomSpan[curr + 8] = 8;
            curr += 9;
        }

        if (includeBoneWeights)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 5);
            geomSpan[curr + 8] = 4;
            curr += 9;

            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 5);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 4);
            geomSpan[curr + 8] = 16;
            curr += 9;
        }

        if (unknownDatatype > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), unknownDatatype);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 5);
            geomSpan[curr + 8] = 4;
            curr += 9;
        }

        // Vertex Data
        for (int i = 0; i < vertexCount; i++)
        {
            int vStart = curr + i * strideBytes;
            int vCurr = vStart;

            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 1.0f * (i + 1));
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 2.0f * (i + 1));
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 8, 4), 3.0f * (i + 1));
            vCurr += 12;

            if (includeNormal)
            {
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 0.0f);
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 1.0f);
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 8, 4), 0.0f);
                vCurr += 12;
            }

            if (includeUv)
            {
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 0.5f);
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 0.5f);
                vCurr += 8;
            }

            if (includeBoneWeights)
            {
                geomSpan[vCurr] = 0;
                geomSpan[vCurr + 1] = 0;
                geomSpan[vCurr + 2] = 0;
                geomSpan[vCurr + 3] = 0;
                vCurr += 4;

                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 1.0f);
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 0.0f);
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 8, 4), 0.0f);
                BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 12, 4), 0.0f);
                vCurr += 16;
            }

            if (unknownDatatype > 0)
            {
                vCurr += 4;
            }
        }
        curr += vertexBufferBytes;

        // Face Group Header
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        curr += 4;
        geomSpan[curr] = 0x02;
        curr += 1;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)facePointCount);
        curr += 4;

        // Index Buffer
        ushort idxA = 0;
        ushort idxB = 1;
        ushort idxC = customIndex != 0xFFFF ? customIndex : (ushort)2;

        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr, 2), idxA);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 2, 2), idxB);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 4, 2), idxC);
        curr += indexBufferBytes;

        // Skin Controller (4B), BoneCount (4B)
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)boneCount);
        curr += 4;

        // Bone Hashes (4B)
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0xAAAA0000);
        curr += boneHashBytes;

        // Embedded TGI Tail
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(geomSpan.Slice(curr + 4, 8), 0x9999888877776666UL);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), 0x00B2D882);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 16, 4), 0x00000000);

        return buffer;
    }

    private static byte[] CreateMultiChunkRcolBufferWithGeomSecond()
    {
        uint headerSize = 8;
        int chunk0Offset = 72; // 8B header + 32B internal ITG + 16B ext TGI + 16B location table = 72B
        int chunk0Length = 30; // Fake chunk 0

        int chunk1Offset = 102;
        byte[] validSingleGeom = CreateValidCountFirstGeomBuffer();
        // Extract GEOM chunk 1 payload from valid single buffer (starts at offset 48)
        byte[] geomChunk1Bytes = new byte[validSingleGeom.Length - 48];
        Array.Copy(validSingleGeom, 48, geomChunk1Bytes, 0, geomChunk1Bytes.Length);

        int totalLength = chunk1Offset + geomChunk1Bytes.Length;
        var buffer = new byte[totalLength];
        var span = buffer.AsSpan();

        // 1. RCOL Header (Count-First): ExtCount = 1, IntCount = 2
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 2);

        // 2. Internal ITG 0 (offset 8): Non-GEOM TypeId 0x11223344 (contains "GEOM" ASCII in payload)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), 0x1111111122222222UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x11223344);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0x00000000);

        // Internal ITG 1 (offset 24): GEOM TypeId 0x015A1849
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), 0x5555666677778888UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x015A1849);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), 0x00000000);

        // 3. External TGI (16B) at offset 40
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(40, 8), 0x9999999988888888UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(48, 4), 0x00B2D882);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(52, 4), 0x00000000);

        // 4. Location Table (16B) at offset 56:
        // Chunk 0 Location: Position (72 - 8 = 64), Size 30
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(56, 4), (uint)(chunk0Offset - (int)headerSize));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(60, 4), (uint)chunk0Length);

        // Chunk 1 Location: Position (102 - 8 = 94), Size geomChunk1Bytes.Length
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(64, 4), (uint)(chunk1Offset - (int)headerSize));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(68, 4), (uint)geomChunk1Bytes.Length);

        // Write Chunk 0 Fake Data at offset 72 (put "GEOM" ASCII in payload to test candidate validation!)
        Encoding.ASCII.GetBytes("FAKEGEOMPAYLOAD_TEST_DATA_BLAH", span.Slice(chunk0Offset, chunk0Length));

        // Write Chunk 1 GEOM Data at offset 102
        Array.Copy(geomChunk1Bytes, 0, buffer, chunk1Offset, geomChunk1Bytes.Length);

        return buffer;
    }

    [Fact]
    public void Import_MultiChunkRcolWithNonGeomFirstChunk_SelectsCorrectGeomChunk()
    {
        // Arrange: Multi-chunk RCOL payload where Chunk 0 is Non-GEOM (with fake "GEOM" string) and Chunk 1 is real GEOM
        var buffer = CreateMultiChunkRcolBufferWithGeomSecond();

        // Act
        var result = _importer.Import(buffer, "MultiChunkMesh");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();
        result.Mesh!.Name.Should().Be("MultiChunkMesh");
        result.Mesh.Vertices.Count.Should().Be(3);
        result.Mesh.Faces.Count.Should().Be(1);
    }

    [Fact]
    public void Import_ValidMinimalTriangleGeom_ProducesCanonicalMeshWithVerticesAndFace()
    {
        // Arrange
        var buffer = CreateValidCountFirstGeomBuffer(includeNormal: true, includeUv: true);

        // Act
        var result = _importer.Import(buffer, "TestMesh");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();
        result.Mesh!.Name.Should().Be("TestMesh");
        result.Mesh.Vertices.Count.Should().Be(3);
        result.Mesh.Faces.Count.Should().Be(1);
        result.Mesh.SourceGameVersion.Should().Be(GameVersion.Sims3);
        result.Mesh.CoordinateSystem.Should().Be(CanonicalCoordinateSystem.RightHandedYUp);
    }

    [Fact]
    public void Import_PositionNormalUv_DecodesDeterministicFloatValues()
    {
        // Arrange
        var buffer = CreateValidCountFirstGeomBuffer(includeNormal: true, includeUv: true);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();

        var v0 = result.Mesh!.Vertices[0];
        v0.Position.Should().Be(new MeshVector3(1.0f, 2.0f, 3.0f));
        v0.Normal.Should().Be(new MeshVector3(0.0f, 1.0f, 0.0f));
        v0.Uv0.Should().Be(new MeshVector2(0.5f, 0.5f));

        var face = result.Mesh.Faces[0];
        face.A.Should().Be(0);
        face.B.Should().Be(1);
        face.C.Should().Be(2);
    }

    [Fact]
    public void Import_PositionOnlyMesh_ProducesMeshWithWarnings()
    {
        // Arrange: Position descriptor only
        var buffer = CreateValidCountFirstGeomBuffer(includeNormal: false, includeUv: false);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();
        result.Mesh!.Vertices.Count.Should().Be(3);
        result.Mesh.Vertices[0].Normal.Should().BeNull();
        result.Mesh.Vertices[0].Uv0.Should().BeNull();
    }

    [Fact]
    public void Import_BoneAssignmentsAndWeights_ProducesCanonicalBoneWeights()
    {
        // Arrange: Include Bone Indices (DataType 4) and Weights (DataType 5)
        var buffer = CreateValidCountFirstGeomBuffer(includeNormal: true, includeUv: true, includeBoneWeights: true);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();

        var v0 = result.Mesh!.Vertices[0];
        v0.BoneWeights.Should().NotBeNull();
        v0.BoneWeights!.Count.Should().Be(1);
        v0.BoneWeights[0].BoneIndex.Should().Be(0);
        v0.BoneWeights[0].Weight.Should().Be(1.0f);
    }

    [Fact]
    public void Import_IndexOutOfRange_TriggersValidatorIssue()
    {
        // Arrange: customIndex = 99 (out of range when vertexCount = 3)
        var buffer = CreateValidCountFirstGeomBuffer(customIndex: 99);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse("Index 99 out of range for 3 vertices MUST trigger validator error MESHV003");
        result.Issues.Should().Contain(i => i.Code == "MESHV003");
    }

    [Fact]
    public void Import_TruncatedVertexBuffer_ReturnsControlledError()
    {
        // Arrange: Buffer truncated inside vertex data
        var fullBuffer = CreateValidCountFirstGeomBuffer();
        var truncatedBuffer = new byte[fullBuffer.Length - 50];
        Array.Copy(fullBuffer, truncatedBuffer, truncatedBuffer.Length);

        // Act
        var result = _importer.Import(truncatedBuffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "GEOM004" || i.Code == "GEOM005" || i.Code == "MESHG002");
    }

    [Fact]
    public void Import_TruncatedIndexBuffer_ReturnsControlledError()
    {
        // Arrange: Buffer truncated inside index data
        var fullBuffer = CreateValidCountFirstGeomBuffer();
        var truncatedBuffer = new byte[fullBuffer.Length - 25];
        Array.Copy(fullBuffer, truncatedBuffer, truncatedBuffer.Length);

        // Act
        var result = _importer.Import(truncatedBuffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "GEOM004" || i.Code == "GEOM005" || i.Code == "MESHG002");
    }

    [Fact]
    public void Import_UnknownVertexDescriptorDatatype_ProducesDiagnosticIssue()
    {
        // Arrange: Include unknown descriptor datatype 99
        var buffer = CreateValidCountFirstGeomBuffer(unknownDatatype: 99);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "MESHG003" && i.Severity == ConversionIssueSeverity.Warning);
    }

    [Fact]
    public void Import_NullOrEmptyBuffer_ReturnsControlledError()
    {
        // Act
        var result = _importer.Import(ReadOnlySpan<byte>.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MESHG000");
    }

    [Fact]
    public async Task ImportAsync_NullStream_ReturnsControlledError()
    {
        // Act
        var result = await _importer.ImportAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MESHG000");
    }
}
