using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.App.Services;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.App.Tests;

public class ResourceInspectorViewModelMeshTests
{
    private readonly MeshResourceClassifier _meshClassifier = new();
    private readonly Ts3GeomCanonicalMeshImporter _geomImporter = new();
    private readonly Ts4GeomCanonicalMeshImporter _ts4Importer = new(new Ts4GeomMetadataReader(), new CanonicalMeshValidator());

    private IMeshInspectionService CreateMeshInspectionService(PackageInspectionResult packageResult, bool provideValidGeomPayload = false)
    {
        var fakePkgService = new FakeInspectionService(packageResult);
        var payloadReader = provideValidGeomPayload
            ? (SimsConverter.Package.Contracts.IPackageResourcePayloadReader)new FakeValidGeomPayloadReader(CreateValidCountFirstGeomPayload())
            : new FakeFailingPayloadReader();

        return new MeshInspectionService(fakePkgService, _meshClassifier, _geomImporter, _ts4Importer, payloadReader);
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

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), 0x5555666677778888UL);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x015A1849);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0x00000000);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), 0x1111222233334444UL);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x00B2D882);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), 0x00000000);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)(geomChunkOffset - (int)headerSize));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), (uint)geomChunkLength);

        var geomSpan = span.Slice(geomChunkOffset);
        System.Text.Encoding.ASCII.GetBytes("GEOM", geomSpan.Slice(0, 4));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(4, 4), 5);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(8, 4), rawTgiOffset);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(12, 4), tgiSize);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0);

        int curr = 20;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;

        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)vertexCount);
        curr += 4;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)elementCount);
        curr += 4;

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 3);
        geomSpan[curr + 8] = 12;
        curr += 9;

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 3);
        geomSpan[curr + 8] = 12;
        curr += 9;

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 3);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 2);
        geomSpan[curr + 8] = 8;
        curr += 9;

        for (int i = 0; i < vertexCount; i++)
        {
            int vCurr = curr + i * strideBytes;
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 1.0f * (i + 1));
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 2.0f * (i + 1));
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 8, 4), 3.0f * (i + 1));
            vCurr += 12;

            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 0.0f);
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 1.0f);
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 8, 4), 0.0f);
            vCurr += 12;

            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr, 4), 0.5f);
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vCurr + 4, 4), 0.5f);
        }
        curr += vertexBufferBytes;

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        curr += 4;
        geomSpan[curr] = 0x02;
        curr += 1;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)facePointCount);
        curr += 4;

        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr, 2), 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 2, 2), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 4, 2), 2);
        curr += indexBufferBytes;

        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)boneCount);
        curr += 4;

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0xAAAA0000);
        curr += boneHashBytes;

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(geomSpan.Slice(curr + 4, 8), 0x9999888877776666UL);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), 0x00B2D882);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 16, 4), 0x00000000);

        return buffer;
    }

    [Fact]
    public async Task InspectAsync_GivenValidPackage_PopulatesMeshResources()
    {
        // Arrange
        var geomRow = new PackageResourceRow(
            MeshTypeIds.Ts3Geom,
            0x00000000u,
            0x123456789ABCDEF0UL,
            "0x015A1849",
            "0x00000000",
            "0x123456789ABCDEF0",
            "015A1849:00000000:123456789ABCDEF0",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubResult = new PackageInspectionResult(
            true,
            "/path/to/mesh.package",
            new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
            new[] { geomRow },
            Array.Empty<ConversionIssue>()
        );

        var stubPkgService = new FakeInspectionService(stubResult);
        var meshInspectionService = CreateMeshInspectionService(stubResult, provideValidGeomPayload: true);

        var viewModel = new ResourceInspectorViewModel(
            stubPkgService,
            meshInspectionService: meshInspectionService)
        {
            SelectedFilePath = "/path/to/mesh.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsBusy.Should().BeFalse();
        viewModel.IsSims3PackMode.Should().BeFalse();
        viewModel.Resources.Should().ContainSingle();
        viewModel.MeshResources.Should().ContainSingle();
        viewModel.HasMeshResources.Should().BeTrue();
        viewModel.StatusMessage.Should().Contain("1 mesh candidates");
    }

    [Fact]
    public async Task SelectedMeshResource_KnownTs3Geom_SetsCanInspectSelectedMeshTrue()
    {
        // Arrange
        var geomRow = new PackageResourceRow(
            MeshTypeIds.Ts3Geom,
            0x00000000u,
            0x123456789ABCDEF0UL,
            "0x015A1849",
            "0x00000000",
            "0x123456789ABCDEF0",
            "015A1849:00000000:123456789ABCDEF0",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubResult = new PackageInspectionResult(
            true,
            "/path/to/mesh.package",
            new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
            new[] { geomRow },
            Array.Empty<ConversionIssue>()
        );

        var stubPkgService = new FakeInspectionService(stubResult);
        var meshInspectionService = CreateMeshInspectionService(stubResult, provideValidGeomPayload: true);

        var viewModel = new ResourceInspectorViewModel(
            stubPkgService,
            meshInspectionService: meshInspectionService)
        {
            SelectedFilePath = "/path/to/mesh.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);
        viewModel.SelectedMeshResource = viewModel.MeshResources[0];

        // Assert: ViewModel selection command state
        viewModel.SelectedMeshResource.Should().NotBeNull();
        viewModel.CanInspectSelectedMesh.Should().BeTrue("CanInspectSelectedMesh MUST evaluate to true when a valid TS3 GEOM mesh row is selected.");

        // Assert: Summary fields projected to UI presentation model
        var row = viewModel.SelectedMeshResource!;
        row.ClassificationKind.Should().Be(MeshClassificationKind.KnownMesh);
        row.CanInspectCanonicalMesh.Should().BeTrue();
        row.VertexCount.Should().Be(3);
        row.FaceCount.Should().Be(1);
        row.BoneCount.Should().Be(0);
        row.HasNormals.Should().BeTrue();
        row.HasUv0.Should().BeTrue();
        row.HasBoneWeights.Should().BeFalse();
        row.ValidationIssueCount.Should().Be(0);
    }

    [Fact]
    public async Task InspectAsync_UnknownMeshRow_PreservedInMeshResources()
    {
        // Arrange
        var unknownRow = new PackageResourceRow(
            0x88888888u,
            0x00000000u,
            0x8888UL,
            "0x88888888",
            "0x00000000",
            "0x0000000000008888",
            "88888888:00000000:0000000000008888",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubResult = new PackageInspectionResult(
            true,
            "/path/to/test.package",
            new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
            new[] { unknownRow },
            Array.Empty<ConversionIssue>()
        );

        var stubPkgService = new FakeInspectionService(stubResult);
        var meshInspectionService = CreateMeshInspectionService(stubResult);

        var viewModel = new ResourceInspectorViewModel(
            stubPkgService,
            meshInspectionService: meshInspectionService)
        {
            SelectedFilePath = "/path/to/test.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.MeshResources.Should().ContainSingle();
        var row = viewModel.MeshResources[0];
        row.ClassificationKind.Should().Be(MeshClassificationKind.Unknown);
        row.Issues.Should().Contain(i => i.Code == "MESHC001");
    }

    [Fact]
    public async Task InspectAsync_GivenInvalidPackage_ClearsMeshResources()
    {
        // Arrange
        var issue = new ConversionIssue("PARSE002", "Header magic invalid", ConversionIssueSeverity.Error);
        var stubResult = new PackageInspectionResult(
            false,
            "/path/to/corrupt.package",
            null,
            Array.Empty<PackageResourceRow>(),
            new[] { issue }
        );

        var stubPkgService = new FakeInspectionService(stubResult);
        var meshInspectionService = CreateMeshInspectionService(stubResult);

        var viewModel = new ResourceInspectorViewModel(
            stubPkgService,
            meshInspectionService: meshInspectionService)
        {
            SelectedFilePath = "/path/to/corrupt.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsBusy.Should().BeFalse();
        viewModel.HasIssues.Should().BeTrue();
        viewModel.Resources.Should().BeEmpty();
        viewModel.MeshResources.Should().BeEmpty();
        viewModel.HasMeshResources.Should().BeFalse();
    }

    [Fact]
    public async Task InspectAsync_GivenSims3PackFile_DoesNotPopulateMeshResources()
    {
        // Arrange
        var stubPayloadRow = new Sims3PackPayloadRow(
            EntryIndex: 1,
            Kind: "DbpfPackage",
            DataOffset: 500,
            DataOffsetHex: "0x000001F4",
            EstimatedSizeBytes: 1024,
            EstimatedSizeFormatted: "1.0 KB",
            DisplayName: "Villa Package",
            CanExport: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var stubResult = new Sims3PackInspectionResult(
            IsSuccess: true,
            FilePath: "/path/to/villa.sims3pack",
            RootElementName: "Sims3Pack",
            DeclaredEncoding: "utf-8",
            RawXmlSizeBytes: 150,
            Title: "Modern Villa",
            AssetId: "GUID-123",
            AssetType: "Lot",
            Description: "A villa",
            PayloadRows: new[] { stubPayloadRow },
            Issues: Array.Empty<ConversionIssue>()
        );

        var fakeDbpfService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeS3PService = new FakeSims3PackInspectionService(stubResult);

        var viewModel = new ResourceInspectorViewModel(fakeDbpfService, sims3PackInspectionService: fakeS3PService)
        {
            SelectedFilePath = "/path/to/villa.sims3pack"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsSims3PackMode.Should().BeTrue();
        viewModel.MeshResources.Should().BeEmpty();
        viewModel.HasMeshResources.Should().BeFalse();
    }

    [Fact]
    public void AppAssembly_ContainsNoProhibitedBinaryParsingReferences()
    {
        // Arrange
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string appSourceDir = Path.Combine(solutionDir, "src", "SimsConverter.App");

        Directory.Exists(appSourceDir).Should().BeTrue("SimsConverter.App source directory must exist");

        string[] csFiles = Directory.GetFiles(appSourceDir, "*.cs", SearchOption.AllDirectories);
        csFiles.Should().NotBeEmpty();

        var forbiddenSymbols = new[] { "BinaryPrimitives", "System.Buffers.Binary", "FileStream", "ReadAllBytes" };

        foreach (var file in csFiles)
        {
            if (file.Contains(Path.Combine("obj", "Debug")) || file.Contains(Path.Combine("obj", "Release")))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            foreach (var forbidden in forbiddenSymbols)
            {
                content.Should().NotContain(forbidden, $"File '{file}' in SimsConverter.App must not contain low-level binary symbol '{forbidden}'");
            }
        }
    }

    private class FakeInspectionService : IPackageInspectionService
    {
        private readonly PackageInspectionResult _result;

        public FakeInspectionService(PackageInspectionResult result)
        {
            _result = result;
        }

        public Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private class FakeSims3PackInspectionService : ISims3PackInspectionService
    {
        private readonly Sims3PackInspectionResult _inspectionResult;

        public FakeSims3PackInspectionService(Sims3PackInspectionResult inspectionResult)
        {
            _inspectionResult = inspectionResult;
        }

        public Task<Sims3PackInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_inspectionResult);
        }

        public Task<Sims3PackExportResult> ExportPayloadAsync(Sims3PackExportRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Sims3PackExportResult.Failure("", "", "ERR", "Error"));
        }
    }

    private class FakeValidGeomPayloadReader : SimsConverter.Package.Contracts.IPackageResourcePayloadReader
    {
        private readonly byte[] _payload;

        public FakeValidGeomPayloadReader(byte[] payload)
        {
            _payload = payload;
        }

        public PackageResourcePayloadResult ReadPayload(string packageFilePath, PackageResourceEntry entry)
        {
            return new PackageResourcePayloadResult(true, _payload, Array.Empty<ConversionIssue>());
        }

        public Task<PackageResourcePayloadResult> ReadPayloadAsync(string packageFilePath, PackageResourceEntry entry, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageResourcePayloadResult(true, _payload, Array.Empty<ConversionIssue>()));
        }
    }

    private class FakeFailingPayloadReader : SimsConverter.Package.Contracts.IPackageResourcePayloadReader
    {
        public PackageResourcePayloadResult ReadPayload(string packageFilePath, PackageResourceEntry entry)
        {
            return new PackageResourcePayloadResult(false, null, Array.Empty<ConversionIssue>());
        }

        public Task<PackageResourcePayloadResult> ReadPayloadAsync(string packageFilePath, PackageResourceEntry entry, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageResourcePayloadResult(false, null, Array.Empty<ConversionIssue>()));
        }
    }
}
