using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Mesh.Tests;

public class Ts4GeomCanonicalMeshImporterTests
{
    private readonly Ts4GeomCanonicalMeshImporter _importer;

    public Ts4GeomCanonicalMeshImporterTests()
    {
        var metadataReader = new Ts4GeomMetadataReader();
        var validator = new CanonicalMeshValidator();
        _importer = new Ts4GeomCanonicalMeshImporter(metadataReader, validator);
    }

    private static byte[] CreateValidTs4GeomPayload(
        uint geomVersion = 12,
        int vertexCount = 3,
        int facePointCount = 3,
        byte bytesPerFacePoint = 2,
        bool includePosition = true,
        uint extraDatatype = 0,
        int boneCount = 1,
        uint boneWeightFormat = 2, // 1 = Float4 (16B), 2 = Byte4 normalized (4B)
        byte? overrideWeightSizeBytes = null,
        int truncateAtBytes = -1)
    {
        var descriptors = new List<(uint datatype, uint format, byte size)>();
        if (includePosition)
        {
            descriptors.Add((1, 3, 12)); // Position float3 (12B)
        }
        descriptors.Add((2, 3, 12)); // Normal float3 (12B)
        descriptors.Add((3, 2, 8));  // UV0 float2 (8B)
        descriptors.Add((4, 4, 4));  // Bone Indices byte4 (4B)

        byte weightSize = overrideWeightSizeBytes.HasValue
            ? overrideWeightSizeBytes.Value
            : (boneWeightFormat == 1 ? (byte)16 : (byte)4);
        descriptors.Add((5, boneWeightFormat, weightSize)); // Bone Weights

        if (extraDatatype > 0)
        {
            descriptors.Add((extraDatatype, 1, 4));
        }

        int elementCount = descriptors.Count;
        int strideBytes = 0;
        foreach (var desc in descriptors)
        {
            strideBytes += desc.size;
        }

        int vertexBufferBytes = vertexCount * strideBytes;
        int facePointSize = bytesPerFacePoint == 0 ? 2 : bytesPerFacePoint;
        int indexBufferBytes = facePointCount * facePointSize;
        int boneHashBytes = boneCount * 4;
        int submeshBytes = 4 + 1 + 4 + indexBufferBytes;

        int stitchesBytes = 0;
        if (geomVersion == 5)
        {
            stitchesBytes = 4;
        }
        else if (geomVersion >= 12)
        {
            stitchesBytes += 4; // uvStitchCount = 0
            if (geomVersion >= 13)
            {
                stitchesBytes += 4; // seamStitchCount = 0
            }
            stitchesBytes += 4; // slotrayCount = 0
        }

        int boneSectionBytes = 4 + boneHashBytes;
        int tailTgiBytes = 4 + 16;

        int geomChunkLength = 20 + 16 + (elementCount * 9) + vertexBufferBytes + submeshBytes + stitchesBytes + boneSectionBytes + tailTgiBytes;
        int tailStartPos = geomChunkLength - tailTgiBytes;
        uint rawTgiOffset = (uint)(tailStartPos - 12);
        uint tgiSize = (uint)tailTgiBytes;

        int finalBufferLength = truncateAtBytes > 0 ? truncateAtBytes : geomChunkLength;
        var buffer = new byte[finalBufferLength];
        var span = buffer.AsSpan();

        if (span.Length >= 4) Encoding.ASCII.GetBytes("GEOM", span.Slice(0, 4));
        if (span.Length >= 8) BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), geomVersion);
        if (span.Length >= 12) BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), rawTgiOffset);
        if (span.Length >= 16) BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), tgiSize);
        if (span.Length >= 20) BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0);

        int curr = 20;

        if (curr + 16 <= span.Length)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr + 4, 4), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 8, 4), (uint)vertexCount);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 12, 4), (uint)elementCount);
            curr += 16;
        }

        foreach (var desc in descriptors)
        {
            if (curr + 9 <= span.Length)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), desc.datatype);
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 4, 4), desc.format);
                span[curr + 8] = desc.size;
                curr += 9;
            }
        }

        // Vertex buffer floats
        for (int v = 0; v < vertexCount; v++)
        {
            int vStart = curr + (v * strideBytes);
            int ePos = 0;

            foreach (var desc in descriptors)
            {
                if (vStart + ePos + desc.size <= span.Length)
                {
                    if (desc.datatype == 1) // Position
                    {
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos, 4), (float)(v * 1.0 + 1.0));
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 4, 4), (float)(v * 2.0 + 1.0));
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 8, 4), (float)(v * 3.0 + 1.0));
                    }
                    else if (desc.datatype == 2) // Normal
                    {
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos, 4), 0.0f);
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 4, 4), 1.0f);
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 8, 4), 0.0f);
                    }
                    else if (desc.datatype == 3) // UV0
                    {
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos, 4), 0.25f);
                        BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 4, 4), 0.75f);
                    }
                    else if (desc.datatype == 4) // Bone Indices
                    {
                        span[vStart + ePos] = 2;
                        span[vStart + ePos + 1] = 5;
                        span[vStart + ePos + 2] = 0;
                        span[vStart + ePos + 3] = 0;
                    }
                    else if (desc.datatype == 5) // Bone Weights
                    {
                        if (boneWeightFormat == 1 && desc.size >= 16) // Float4
                        {
                            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos, 4), 0.8f);
                            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 4, 4), 0.2f);
                            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 8, 4), 0.0f);
                            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + ePos + 12, 4), 0.0f);
                        }
                        else if (desc.size >= 4) // Byte4 normalized
                        {
                            span[vStart + ePos] = 204;
                            span[vStart + ePos + 1] = 51;
                            span[vStart + ePos + 2] = 0;
                            span[vStart + ePos + 3] = 0;
                        }
                    }
                }
                ePos += desc.size;
            }
        }
        curr += vertexBufferBytes;

        // Submesh Section
        if (curr + 5 <= span.Length)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 1); // numSubMeshes = 1
            curr += 4;
            span[curr] = bytesPerFacePoint;
            curr += 1;
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), (uint)facePointCount);
            curr += 4;
        }

        // Indices
        if (curr + indexBufferBytes <= span.Length)
        {
            for (int p = 0; p < facePointCount; p++)
            {
                int val = p % vertexCount;
                int pos = curr + (p * facePointSize);
                if (facePointSize == 1)
                {
                    span[pos] = (byte)val;
                }
                else if (facePointSize == 2)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(pos, 2), (ushort)val);
                }
                else if (facePointSize == 4)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(pos, 4), (uint)val);
                }
            }
            curr += indexBufferBytes;
        }

        // Stitches & Slotrays
        if (geomVersion >= 12 && curr + 4 <= span.Length)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0); // uvStitchCount = 0
            curr += 4;

            if (geomVersion >= 13 && curr + 4 <= span.Length)
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0); // seamStitchCount = 0
                curr += 4;
            }

            if (curr + 4 <= span.Length)
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0); // slotrayCount = 0
                curr += 4;
            }
        }

        // Bone Section
        if (curr + 4 <= span.Length)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), boneCount);
            curr += 4;
            if (curr + boneHashBytes <= span.Length)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 0xAAAA0000);
                curr += boneHashBytes;
            }
        }

        // Tail TGI
        if (curr + 20 <= span.Length)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 1);
            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(curr + 4, 8), 0x9999888877776666UL);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 12, 4), 0x00B2D882);
        }

        return buffer;
    }

    [Fact]
    public void Import_ValidTs4GeomV12Payload_ReturnsCanonicalMeshSuccessWithExplicitValueAssertions()
    {
        // Arrange
        var buffer = CreateValidTs4GeomPayload(geomVersion: 12, vertexCount: 3, facePointCount: 3);

        // Act
        var result = _importer.Import(buffer, "TestMeshV12");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();

        var mesh = result.Mesh!;
        mesh.Name.Should().Be("TestMeshV12");
        mesh.SourceGameVersion.Should().Be(GameVersion.Sims4);
        mesh.CoordinateSystem.Should().Be(CanonicalCoordinateSystem.RightHandedYUp);
        mesh.Vertices.Should().HaveCount(3);

        // Vertex 0 Value Assertions
        var v0 = mesh.Vertices[0];
        v0.Position.X.Should().BeApproximately(1.0f, 0.0001f);
        v0.Position.Y.Should().BeApproximately(1.0f, 0.0001f);
        v0.Position.Z.Should().BeApproximately(1.0f, 0.0001f);

        v0.Normal.Should().NotBeNull();
        v0.Normal!.Value.Y.Should().BeApproximately(1.0f, 0.0001f);

        v0.Uv0.Should().NotBeNull();
        v0.Uv0!.Value.X.Should().BeApproximately(0.25f, 0.0001f);
        v0.Uv0!.Value.Y.Should().BeApproximately(0.75f, 0.0001f);

        v0.BoneWeights.Should().HaveCount(2);
        v0.BoneWeights[0].BoneIndex.Should().Be(2);
        v0.BoneWeights[0].Weight.Should().BeApproximately(204.0f / 255.0f, 0.01f);
        v0.BoneWeights[1].BoneIndex.Should().Be(5);

        mesh.Faces.Should().HaveCount(1);
        mesh.Faces[0].A.Should().Be(0);
        mesh.Faces[0].B.Should().Be(1);
        mesh.Faces[0].C.Should().Be(2);
    }

    [Fact]
    public void Import_ValidTs4GeomWithFloat4BoneWeights_DecodesBoneWeightsCorrectly()
    {
        // Arrange: boneWeightFormat = 1 (Float4 = 16 bytes per vertex)
        var buffer = CreateValidTs4GeomPayload(geomVersion: 12, vertexCount: 3, facePointCount: 3, boneWeightFormat: 1);

        // Act
        var result = _importer.Import(buffer, "Float4WeightsMesh");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();

        var v0 = result.Mesh!.Vertices[0];
        v0.BoneWeights.Should().HaveCount(2);
        v0.BoneWeights[0].BoneIndex.Should().Be(2);
        v0.BoneWeights[0].Weight.Should().BeApproximately(0.8f, 0.0001f);
        v0.BoneWeights[1].BoneIndex.Should().Be(5);
        v0.BoneWeights[1].Weight.Should().BeApproximately(0.2f, 0.0001f);
    }

    [Fact]
    public void Import_ValidTs4GeomWithByte4BoneWeights_DecodesBoneWeightsCorrectly()
    {
        // Arrange: boneWeightFormat = 2 (Byte4 normalized = 4 bytes per vertex)
        var buffer = CreateValidTs4GeomPayload(geomVersion: 12, vertexCount: 3, facePointCount: 3, boneWeightFormat: 2);

        // Act
        var result = _importer.Import(buffer, "Byte4WeightsMesh");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();

        var v0 = result.Mesh!.Vertices[0];
        v0.BoneWeights.Should().HaveCount(2);
        v0.BoneWeights[0].BoneIndex.Should().Be(2);
        v0.BoneWeights[0].Weight.Should().BeApproximately(204.0f / 255.0f, 0.01f);
        v0.BoneWeights[1].BoneIndex.Should().Be(5);
        v0.BoneWeights[1].Weight.Should().BeApproximately(51.0f / 255.0f, 0.01f);
    }

    [Fact]
    public void Import_UnsupportedBoneWeightFormat_ReturnsControlledError()
    {
        // Arrange: boneWeightFormat = 99
        var buffer = CreateValidTs4GeomPayload(boneWeightFormat: 99);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG008");
    }

    [Fact]
    public void Import_Format1InsufficientSizeBytes_ReturnsControlledError()
    {
        // Arrange: boneWeightFormat = 1 (Float4), but override sizeBytes = 8 (< 16B)
        var buffer = CreateValidTs4GeomPayload(boneWeightFormat: 1, overrideWeightSizeBytes: 8);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG008");
    }

    [Fact]
    public void Import_Format2InsufficientSizeBytes_ReturnsControlledError()
    {
        // Arrange: boneWeightFormat = 2 (Byte4), but override sizeBytes = 2 (< 4B)
        var buffer = CreateValidTs4GeomPayload(boneWeightFormat: 2, overrideWeightSizeBytes: 2);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG008");
    }

    [Theory]
    [InlineData(13u)]
    [InlineData(14u)]
    public void Import_ValidTs4GeomV13AndV14Payload_DecodesAccuratelyAfterTailBlocks(uint version)
    {
        // Arrange
        var buffer = CreateValidTs4GeomPayload(geomVersion: version, vertexCount: 3, facePointCount: 3);

        // Act
        var result = _importer.Import(buffer, $"TestMeshV{version}");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();
        result.Mesh!.SourceGameVersion.Should().Be(GameVersion.Sims4);
        result.Mesh.Vertices.Should().HaveCount(3);
    }

    [Fact]
    public void Import_UInt16IndexBuffer_DecodesFaceListCorrectly()
    {
        // Arrange: bytesPerFacePoint = 2 (UInt16)
        var buffer = CreateValidTs4GeomPayload(bytesPerFacePoint: 2);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();
        result.Mesh!.Faces.Should().HaveCount(1);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)4)]
    public void Import_UInt8AndUInt32IndexBuffers_SupportedAndDecodedCorrectly(byte bytesPerPoint)
    {
        // Arrange: bytesPerFacePoint = 1 (UInt8) or 4 (UInt32)
        var buffer = CreateValidTs4GeomPayload(bytesPerFacePoint: bytesPerPoint);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();
        result.Mesh!.Faces.Should().HaveCount(1);
    }

    [Fact]
    public void Import_MissingPositionDescriptor_ReturnsControlledError()
    {
        // Arrange: includePosition = false
        var buffer = CreateValidTs4GeomPayload(includePosition: false);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG004");
    }

    [Fact]
    public void Import_UnknownVertexDatatype_ReturnsWarningAndAdvancesCursorAccurately()
    {
        // Arrange: extraDatatype = 99
        var buffer = CreateValidTs4GeomPayload(extraDatatype: 99);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Mesh.Should().NotBeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG003");
    }

    [Fact]
    public void Import_TruncatedVertexBuffer_ReturnsControlledError()
    {
        // Arrange: truncate inside vertex buffer (at byte 60)
        var buffer = CreateValidTs4GeomPayload(truncateAtBytes: 60);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG002" || i.Code == "GEOM005");
    }

    [Fact]
    public void Import_TruncatedIndexBuffer_ReturnsControlledError()
    {
        // Arrange: truncate inside index buffer (at byte 165)
        var buffer = CreateValidTs4GeomPayload(truncateAtBytes: 165);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG002" || i.Code == "GEOM005");
    }

    [Fact]
    public void Import_InvalidBytesPerFacePoint_ReturnsControlledError()
    {
        // Arrange: bytesPerFacePoint = 5
        var buffer = CreateValidTs4GeomPayload(bytesPerFacePoint: 5);

        // Act
        var result = _importer.Import(buffer);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MESHG007");
    }

    [Fact]
    public void Import_DoesNotMutateSourceBuffer()
    {
        // Arrange
        var original = CreateValidTs4GeomPayload();
        var clone = (byte[])original.Clone();

        // Act
        _importer.Import(original);

        // Assert
        original.Should().Equal(clone, "Import MUST remain read-only and MUST NOT mutate the source buffer.");
    }
}
