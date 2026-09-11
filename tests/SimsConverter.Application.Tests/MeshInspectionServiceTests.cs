using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Application.Tests;

public class MeshInspectionServiceTests
{
    private readonly MeshInspectionService _service;
    private readonly MeshResourceClassifier _classifier = new();
    private readonly Ts3GeomCanonicalMeshImporter _ts3Importer = new();
    private readonly Ts4GeomCanonicalMeshImporter _ts4Importer = new(new Ts4GeomMetadataReader(), new CanonicalMeshValidator());

    public MeshInspectionServiceTests()
    {
        var dummyPackageService = new DummyPackageInspectionService();
        var payloadReader = new PackageResourcePayloadReader();
        _service = new MeshInspectionService(dummyPackageService, _classifier, _ts3Importer, _ts4Importer, payloadReader);
    }

    private static byte[] CreateValidCountFirstGeomPayload()
    {
        uint headerSize = 8;
        int geomChunkOffset = 48;
        int vertexCount = 3;
        int facePointCount = 3;
        int boneCount = 1;
        int elementCount = 3;
        int strideBytes = 32;

        int vertexBufferBytes = vertexCount * strideBytes;
        int indexBufferBytes = facePointCount * 2;
        int boneHashBytes = boneCount * 4;
        int faceGroupHeaderBytes = 9;
        int embeddedTgiSizeBytes = 20;

        int geomChunkLength = 20 + 4 + 16 + (elementCount * 9) + vertexBufferBytes + faceGroupHeaderBytes + indexBufferBytes + 8 + boneHashBytes + embeddedTgiSizeBytes;
        int tailStartPos = geomChunkLength - embeddedTgiSizeBytes;
        uint rawTgiOffset = (uint)(tailStartPos - 12);
        uint tgiSize = (uint)embeddedTgiSizeBytes;

        int totalSizeBytes = geomChunkOffset + geomChunkLength;
        var buffer = new byte[totalSizeBytes];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1);

        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), 0x5555666677778888UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x015A1849);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0x00000000);

        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x00B2D882);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), 0x00000000);

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)(geomChunkOffset - (int)headerSize));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), (uint)geomChunkLength);

        var geomSpan = span.Slice(geomChunkOffset);
        Encoding.ASCII.GetBytes("GEOM", geomSpan.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(4, 4), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(8, 4), rawTgiOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(12, 4), tgiSize);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0);

        int curr = 20;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;

        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)vertexCount);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)elementCount);
        curr += 4;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 3);
        geomSpan[curr + 8] = 12;
        curr += 9;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 3);
        geomSpan[curr + 8] = 12;
        curr += 9;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 2);
        geomSpan[curr + 8] = 8;
        curr += 9;

        for (int i = 0; i < vertexCount; i++)
        {
            int vCurr = curr + i * strideBytes;
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 1.0f * (i + 1));
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 2.0f * (i + 1));
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 8, 4), 3.0f * (i + 1));
            vCurr += 12;

            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 0.0f);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 1.0f);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 8, 4), 0.0f);
            vCurr += 12;

            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 0.5f);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 0.5f);
        }
        curr += vertexBufferBytes;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        curr += 4;
        geomSpan[curr] = 0x02;
        curr += 1;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)facePointCount);
        curr += 4;

        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 4, 2), 2);
        curr += indexBufferBytes;

        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)boneCount);
        curr += 4;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0xAAAA0000);
        curr += boneHashBytes;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(geomSpan.Slice(curr + 4, 8), 0x9999888877776666UL);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), 0x00B2D882);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 16, 4), 0x00000000);

        return buffer;
    }

    private static byte[] CreateValidTs4GeomPayload()
    {
        int vertexCount = 3;
        int facePointCount = 3;
        byte bytesPerFacePoint = 2;

        var descriptors = new List<(uint datatype, uint format, byte size)>
        {
            (1, 3, 12), // Position float3
            (2, 3, 12), // Normal float3
            (3, 2, 8),  // UV0 float2
            (4, 4, 4),  // Bone Indices byte4
            (5, 2, 4)   // Bone Weights byte4
        };

        int elementCount = descriptors.Count;
        int strideBytes = 40;
        int vertexBufferBytes = vertexCount * strideBytes;
        int indexBufferBytes = facePointCount * bytesPerFacePoint;
        int submeshBytes = 4 + 1 + 4 + indexBufferBytes;
        int stitchesBytes = 4 + 4; // uvStitchCount=0, slotrayCount=0
        int boneSectionBytes = 4 + 4;
        int tailTgiBytes = 4 + 16;

        int geomChunkLength = 20 + 16 + (elementCount * 9) + vertexBufferBytes + submeshBytes + stitchesBytes + boneSectionBytes + tailTgiBytes;
        int tailStartPos = geomChunkLength - tailTgiBytes;
        uint rawTgiOffset = (uint)(tailStartPos - 12);
        uint tgiSize = (uint)tailTgiBytes;

        var buffer = new byte[geomChunkLength];
        var span = buffer.AsSpan();

        Encoding.ASCII.GetBytes("GEOM", span.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), rawTgiOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), tgiSize);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0);

        int curr = 20;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr + 4, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 8, 4), (uint)vertexCount);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 12, 4), (uint)elementCount);
        curr += 16;

        foreach (var desc in descriptors)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), desc.datatype);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 4, 4), desc.format);
            span[curr + 8] = desc.size;
            curr += 9;
        }

        for (int v = 0; v < vertexCount; v++)
        {
            int vStart = curr + (v * strideBytes);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart, 4), (float)(v + 1));
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + 4, 4), (float)(v + 1));
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + 8, 4), (float)(v + 1));

            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + 12, 4), 0.0f);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + 16, 4), 1.0f);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + 20, 4), 0.0f);

            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + 24, 4), 0.5f);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(vStart + 28, 4), 0.5f);

            span[vStart + 32] = 1;
            span[vStart + 33] = 0;
            span[vStart + 34] = 0;
            span[vStart + 35] = 0;

            span[vStart + 36] = 255;
            span[vStart + 37] = 0;
            span[vStart + 38] = 0;
            span[vStart + 39] = 0;
        }
        curr += vertexBufferBytes;

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 1); // numSubMeshes = 1
        curr += 4;
        span[curr] = bytesPerFacePoint;
        curr += 1;
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), (uint)facePointCount);
        curr += 4;

        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(curr, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(curr + 2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(curr + 4, 2), 2);
        curr += indexBufferBytes;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0); // uvStitchCount = 0
        curr += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 0); // slotrayCount = 0
        curr += 4;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 1); // boneCount = 1
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr, 4), 0xAAAA0000);
        curr += 4;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(curr + 4, 8), 0x9999888877776666UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(curr + 12, 4), 0x00B2D882);

        return buffer;
    }

    [Fact]
    public async Task InspectPackageMeshesAsync_NullOrEmptyRequest_ReturnsControlledError()
    {
        var result = await _service.InspectPackageMeshesAsync(null!);
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MESHA000");
    }

    [Fact]
    public void InspectPackageMeshes_KnownTs3GeomRow_SetsCanInspectCanonicalMeshTrue()
    {
        var tempFile = Path.GetTempFileName();
        byte[] geomPayload = CreateValidCountFirstGeomPayload();

        var fileBuffer = new byte[96 + geomPayload.Length];
        Encoding.ASCII.GetBytes("DBPF", fileBuffer.AsSpan(0, 4));
        Array.Copy(geomPayload, 0, fileBuffer, 96, geomPayload.Length);
        File.WriteAllBytes(tempFile, fileBuffer);

        try
        {
            var row = new PackageResourceRow(
                TypeId: 0x015A1849,
                GroupId: 0x00000000,
                InstanceId: 0x123456789ABCDEF0,
                TypeHex: "0x015A1849",
                GroupHex: "0x00000000",
                InstanceHex: "0x123456789ABCDEF0",
                FormattedKey: "015A1849-00000000-123456789ABCDEF0",
                Offset: 96,
                CompressedSize: (uint)geomPayload.Length,
                DecompressedSize: (uint)geomPayload.Length,
                CompressionKind: PackageCompressionKind.None,
                CompressionName: "None"
            );

            var pkgInspection = new PackageInspectionResult(
                IsSuccess: true,
                FilePath: tempFile,
                Header: null,
                Resources: new[] { row },
                Issues: Array.Empty<ConversionIssue>()
            );

            var result = _service.InspectPackageMeshes(pkgInspection, GameVersion.Sims3);

            result.IsSuccess.Should().BeTrue();
            result.Rows.Count.Should().Be(1);

            var meshRow = result.Rows[0];
            meshRow.ClassificationKind.Should().Be(MeshClassificationKind.KnownMesh);
            meshRow.RoleKind.Should().Be(MeshRoleKind.Geometry);
            meshRow.CanExtractRawPayload.Should().BeTrue();
            meshRow.CanInspectCanonicalMesh.Should().BeTrue();
            meshRow.VertexCount.Should().Be(3);
            meshRow.FaceCount.Should().Be(1);
            meshRow.HasNormals.Should().BeTrue();
            meshRow.HasUv0.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void InspectPackageMeshes_KnownTs4GeomRow_CallsTs4ImporterAndSetsCanonicalSummaryFieldsCorrectly()
    {
        var tempFile = Path.GetTempFileName();
        byte[] geomPayload = CreateValidTs4GeomPayload();

        var fileBuffer = new byte[96 + geomPayload.Length];
        Encoding.ASCII.GetBytes("DBPF", fileBuffer.AsSpan(0, 4));
        Array.Copy(geomPayload, 0, fileBuffer, 96, geomPayload.Length);
        File.WriteAllBytes(tempFile, fileBuffer);

        try
        {
            var row = new PackageResourceRow(
                TypeId: 0x015A1849,
                GroupId: 0x00000000,
                InstanceId: 0x123456789ABCDEF0,
                TypeHex: "0x015A1849",
                GroupHex: "0x00000000",
                InstanceHex: "0x123456789ABCDEF0",
                FormattedKey: "015A1849-00000000-123456789ABCDEF0",
                Offset: 96,
                CompressedSize: (uint)geomPayload.Length,
                DecompressedSize: (uint)geomPayload.Length,
                CompressionKind: PackageCompressionKind.None,
                CompressionName: "None"
            );

            var pkgInspection = new PackageInspectionResult(
                IsSuccess: true,
                FilePath: tempFile,
                Header: null,
                Resources: new[] { row },
                Issues: Array.Empty<ConversionIssue>()
            );

            var result = _service.InspectPackageMeshes(pkgInspection, GameVersion.Sims4);

            result.IsSuccess.Should().BeTrue();
            result.Rows.Count.Should().Be(1);

            var meshRow = result.Rows[0];
            meshRow.ClassificationKind.Should().Be(MeshClassificationKind.KnownMesh);
            meshRow.DetectedGameVersion.Should().Be(GameVersion.Sims4);
            meshRow.CanExtractRawPayload.Should().BeTrue();
            meshRow.CanInspectCanonicalMesh.Should().BeTrue();
            meshRow.VertexCount.Should().Be(3);
            meshRow.FaceCount.Should().Be(1);
            meshRow.BoneCount.Should().Be(1);
            meshRow.HasNormals.Should().BeTrue();
            meshRow.HasUv0.Should().BeTrue();
            meshRow.HasBoneWeights.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void InspectPackageMeshes_Ts4ImporterFailure_HandlesControlledFailureWithoutCrashing()
    {
        var tempFile = Path.GetTempFileName();
        var truncatedPayload = new byte[25];
        Encoding.ASCII.GetBytes("GEOM", truncatedPayload.AsSpan(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(truncatedPayload.AsSpan(4, 4), 12);

        var fileBuffer = new byte[96 + truncatedPayload.Length];
        Array.Copy(truncatedPayload, 0, fileBuffer, 96, truncatedPayload.Length);
        File.WriteAllBytes(tempFile, fileBuffer);

        try
        {
            var row = new PackageResourceRow(
                TypeId: 0x015A1849,
                GroupId: 0x00000000,
                InstanceId: 0x123456789ABCDEF0,
                TypeHex: "0x015A1849",
                GroupHex: "0x00000000",
                InstanceHex: "0x123456789ABCDEF0",
                FormattedKey: "015A1849-00000000-123456789ABCDEF0",
                Offset: 96,
                CompressedSize: (uint)truncatedPayload.Length,
                DecompressedSize: (uint)truncatedPayload.Length,
                CompressionKind: PackageCompressionKind.None,
                CompressionName: "None"
            );

            var pkgInspection = new PackageInspectionResult(
                IsSuccess: true,
                FilePath: tempFile,
                Header: null,
                Resources: new[] { row },
                Issues: Array.Empty<ConversionIssue>()
            );

            var result = _service.InspectPackageMeshes(pkgInspection, GameVersion.Sims4);

            result.IsSuccess.Should().BeTrue();
            result.Rows.Count.Should().Be(1);

            var meshRow = result.Rows[0];
            meshRow.ClassificationKind.Should().Be(MeshClassificationKind.KnownMesh);
            meshRow.CanInspectCanonicalMesh.Should().BeFalse();
            meshRow.Issues.Should().NotBeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void InspectPackageMeshes_UnknownMeshRow_ReturnsRowWithDiagnosticIssue()
    {
        var row = new PackageResourceRow(
            TypeId: 0x99999999,
            GroupId: 0x00000000,
            InstanceId: 0x123456789ABCDEF0,
            TypeHex: "0x99999999",
            GroupHex: "0x00000000",
            InstanceHex: "0x123456789ABCDEF0",
            FormattedKey: "99999999-00000000-123456789ABCDEF0",
            Offset: 96,
            CompressedSize: 100,
            DecompressedSize: 100,
            CompressionKind: PackageCompressionKind.None,
            CompressionName: "None"
        );

        var pkgInspection = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "dummy.package",
            Header: null,
            Resources: new[] { row },
            Issues: Array.Empty<ConversionIssue>()
        );

        var result = _service.InspectPackageMeshes(pkgInspection);

        result.IsSuccess.Should().BeTrue();
        result.Rows.Count.Should().Be(1);

        var meshRow = result.Rows[0];
        meshRow.ClassificationKind.Should().Be(MeshClassificationKind.Unknown);
        meshRow.RoleKind.Should().Be(MeshRoleKind.Unknown);
        meshRow.CanExtractRawPayload.Should().BeFalse();
        meshRow.CanInspectCanonicalMesh.Should().BeFalse();
        meshRow.Issues.Should().Contain(i => i.Code == "MESHC001");
    }

    private class DummyPackageInspectionService : IPackageInspectionService
    {
        public Task<PackageInspectionResult> InspectFileAsync(string packageFilePath, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageFilePath))
            {
                return Task.FromResult(PackageInspectionResult.Failure(string.Empty, "EXPE000", "Package path is empty."));
            }

            return Task.FromResult(new PackageInspectionResult(
                IsSuccess: true,
                FilePath: packageFilePath,
                Header: null,
                Resources: Array.Empty<PackageResourceRow>(),
                Issues: Array.Empty<ConversionIssue>()
            ));
        }
    }
}
