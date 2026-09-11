using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using Xunit;

namespace SimsConverter.Application.Tests;

public class DecorativeObjectSourceGraphBuilderTests
{
    private class FakePackageInspectionService : IPackageInspectionService
    {
        public PackageInspectionResult ResultToReturn { get; set; } = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "fake.package",
            Header: null,
            Resources: Array.Empty<PackageResourceRow>(),
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private class FakeMeshInspectionService : IMeshInspectionService
    {
        public MeshInspectionResult ResultToReturn { get; set; } = new MeshInspectionResult(
            IsSuccess: true,
            PackageFilePath: "fake.package",
            Rows: Array.Empty<MeshResourceRow>(),
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<MeshInspectionResult> InspectPackageMeshesAsync(MeshInspectionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }

        public MeshInspectionResult InspectPackageMeshes(PackageInspectionResult packageInspection, GameVersion gameVersionHint = GameVersion.Unknown)
        {
            return ResultToReturn;
        }
    }

    private class FakeTextureInspectionService : ITextureInspectionService
    {
        public TextureInspectionResult ResultToReturn { get; set; } = new TextureInspectionResult(
            IsSuccess: true,
            PackageFilePath: "fake.package",
            Rows: Array.Empty<TextureResourceRow>(),
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<TextureInspectionResult> InspectPackageTexturesAsync(TextureInspectionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }

        public TextureInspectionResult InspectPackageTextures(PackageInspectionResult packageInspection, GameVersion gameVersionHint = GameVersion.Unknown)
        {
            return ResultToReturn;
        }
    }

    [Fact]
    public void BuildGraph_WithImportableMesh_ReturnsIsSourceGraphReadyTrue()
    {
        var entry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 1), 128, 100, 200, PackageCompressionKind.Zlib, 0);
        var pkgRow = PackageResourceRow.FromEntry(entry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "sample.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshClassification = new MeshResourceClassification(
            entry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );

        var meshRow = new MeshResourceRow(
            FormattedKey: entry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000001",
            DataOffset: 128, CompressedSize: 100, DecompressedSize: 200, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 100, FaceCount: 50, BoneCount: 2, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: entry, Classification: meshClassification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(
                IsSuccess: true, PackageFilePath: "sample.package", Rows: new[] { meshRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var texService = new FakeTextureInspectionService();
        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.IsSourceGraphReady.Should().BeTrue();
        graph.HasImportableMesh.Should().BeTrue();
        graph.MeshAssets.Should().ContainSingle();
        graph.MeshAssets[0].CanInspectCanonicalMesh.Should().BeTrue();
        graph.MeshAssets[0].DetectedGameVersion.Should().Be(GameVersion.Sims3);
        graph.ResourceLinks.Should().BeEmpty();
    }

    [Fact]
    public void BuildGraph_GivenTs4GeomMeshWithSims4GameVersion_ReturnsIsSourceGraphReadyFalse()
    {
        // Arrange: GEOM entry with TypeId 0x015A1849 but DetectedGameVersion = Sims4
        var entry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 1), 128, 100, 200, PackageCompressionKind.Zlib, 0);
        var pkgRow = PackageResourceRow.FromEntry(entry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "sample_ts4.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshClassification = new MeshResourceClassification(
            entry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS4 Geometry (GEOM)", GameVersion.Sims4, Array.Empty<ConversionIssue>()
        );

        var meshRow = new MeshResourceRow(
            FormattedKey: entry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000001",
            DataOffset: 128, CompressedSize: 100, DecompressedSize: 200, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS4 Geometry (GEOM)",
            DetectedGameVersion: GameVersion.Sims4, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 100, FaceCount: 50, BoneCount: 2, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: entry, Classification: meshClassification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(
                IsSuccess: true, PackageFilePath: "sample_ts4.package", Rows: new[] { meshRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var texService = new FakeTextureInspectionService();
        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        // Act
        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        // Assert: SIMS-CONV-014 supports Sims4 GEOM meshes in source graph
        graph.IsSourceGraphReady.Should().BeTrue();
        graph.HasImportableMesh.Should().BeTrue();
        graph.MeshAssets.Should().HaveCount(1);
    }

    [Fact]
    public void BuildGraph_GivenModlMlodPackageWithoutTs3Geom_ReturnsUnreadyGraphWithIssueCONVG003()
    {
        // Arrange: Package containing MODL (0x01661233), MLOD (0x01D10F34), RIG (0x8EAF13DE), RSLT (0xD3044521)
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var mlodEntry = new PackageResourceEntry(new PackageResourceId(0x01D10F34, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);
        var rigEntry = new PackageResourceEntry(new PackageResourceId(0x8EAF13DE, 0, 3), 300, 50, 100, PackageCompressionKind.Zlib, 0);
        var rsltEntry = new PackageResourceEntry(new PackageResourceId(0xD3044521, 0, 4), 400, 50, 100, PackageCompressionKind.Zlib, 0);

        var pkgRows = new[]
        {
            PackageResourceRow.FromEntry(modlEntry),
            PackageResourceRow.FromEntry(mlodEntry),
            PackageResourceRow.FromEntry(rigEntry),
            PackageResourceRow.FromEntry(rsltEntry)
        };

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "onyx_sample.package", Header: null,
                Resources: pkgRows, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        // Act
        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        // Assert
        graph.IsSourceGraphReady.Should().BeFalse();
        graph.HasImportableMesh.Should().BeFalse();
        graph.Issues.Should().ContainSingle(i => i.Code == "CONVG003");
        graph.Issues[0].Message.Should().Contain("MODL/MLOD decomposition");
        graph.OtherResources.Should().HaveCount(4);
    }

    [Fact]
    public void BuildGraph_NoMeshCandidates_ReturnsIsSourceGraphReadyFalse_WithControlledIssueCONVG002()
    {
        var pkgService = new FakePackageInspectionService();
        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.IsSourceGraphReady.Should().BeFalse();
        graph.HasImportableMesh.Should().BeFalse();
        graph.Issues.Should().Contain(i => i.Code == "CONVG002");
    }

    [Fact]
    public void BuildGraph_MeshCandidateCannotInspectCanonicalMesh_ReturnsIsSourceGraphReadyFalse()
    {
        var entry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 1), 128, 100, 200, PackageCompressionKind.Zlib, 0);
        var pkgRow = PackageResourceRow.FromEntry(entry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "sample.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshClassification = new MeshResourceClassification(
            entry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );

        var meshRow = new MeshResourceRow(
            FormattedKey: entry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000001",
            DataOffset: 128, CompressedSize: 100, DecompressedSize: 200, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: false,
            VertexCount: null, FaceCount: null, BoneCount: null, HasNormals: false, HasUv0: false, HasBoneWeights: false, ValidationIssueCount: 1,
            Issues: new[] { new ConversionIssue("GEOM005", "Truncated payload", ConversionIssueSeverity.Error) }, Entry: entry, Classification: meshClassification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(
                IsSuccess: true, PackageFilePath: "sample.package", Rows: new[] { meshRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var texService = new FakeTextureInspectionService();
        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.IsSourceGraphReady.Should().BeFalse();
        graph.HasImportableMesh.Should().BeFalse();
        graph.MeshAssets.Should().BeEmpty();
    }

    [Fact]
    public void BuildGraph_TextureCandidatesCollectedCorrectly_WithoutMakingGraphReady()
    {
        var texEntry = new PackageResourceEntry(new PackageResourceId(0x00B2D882, 0, 2), 256, 500, 1000, PackageCompressionKind.Zlib, 0);
        var pkgRow = PackageResourceRow.FromEntry(texEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "sample.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var texClassification = new TextureResourceClassification(
            texEntry.Id, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS Texture", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );

        var texRow = new TextureResourceRow(
            FormattedKey: texEntry.Id.FormattedKey, TypeHex: "0x00B2D882", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            FormatName: "DDS Texture", MapKind: TextureMapKind.Diffuse, DetectedGameVersion: GameVersion.Sims3,
            ClassificationKind: TextureClassificationKind.KnownTexture, CanExtractRawPayload: true, CanParseDdsHeader: true,
            Issues: Array.Empty<ConversionIssue>(), Entry: texEntry, Classification: texClassification
        );

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService
        {
            ResultToReturn = new TextureInspectionResult(
                IsSuccess: true, PackageFilePath: "sample.package", Rows: new[] { texRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.HasTextureCandidates.Should().BeTrue();
        graph.IsSourceGraphReady.Should().BeFalse();
        graph.TextureAssets.Should().ContainSingle();
        graph.TextureAssets[0].FormattedKey.Should().Be(texEntry.Id.FormattedKey);
        graph.TextureAssets[0].CanExtractRawPayload.Should().BeTrue();
        graph.ResourceLinks.Should().BeEmpty();
    }

    [Fact]
    public void BuildGraph_DoesNotProduceSpeculativeMeshTextureLinks()
    {
        var geomEntry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 1), 128, 100, 200, PackageCompressionKind.Zlib, 0);
        var texEntry = new PackageResourceEntry(new PackageResourceId(0x00B2D882, 0, 2), 256, 500, 1000, PackageCompressionKind.Zlib, 0);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "sample.package", Header: null,
                Resources: new[] { PackageResourceRow.FromEntry(geomEntry), PackageResourceRow.FromEntry(texEntry) },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshClassification = new MeshResourceClassification(
            geomEntry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );
        var meshRow = new MeshResourceRow(
            FormattedKey: geomEntry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000001",
            DataOffset: 128, CompressedSize: 100, DecompressedSize: 200, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 100, FaceCount: 50, BoneCount: 2, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: geomEntry, Classification: meshClassification
        );

        var texClassification = new TextureResourceClassification(
            texEntry.Id, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS Texture", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );
        var texRow = new TextureResourceRow(
            FormattedKey: texEntry.Id.FormattedKey, TypeHex: "0x00B2D882", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            FormatName: "DDS Texture", MapKind: TextureMapKind.Diffuse, DetectedGameVersion: GameVersion.Sims3,
            ClassificationKind: TextureClassificationKind.KnownTexture, CanExtractRawPayload: true, CanParseDdsHeader: true,
            Issues: Array.Empty<ConversionIssue>(), Entry: texEntry, Classification: texClassification
        );

        var meshService = new FakeMeshInspectionService { ResultToReturn = new MeshInspectionResult(true, "sample.package", new[] { meshRow }, Array.Empty<ConversionIssue>()) };
        var texService = new FakeTextureInspectionService { ResultToReturn = new TextureInspectionResult(true, "sample.package", new[] { texRow }, Array.Empty<ConversionIssue>()) };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.MeshAssets.Should().ContainSingle();
        graph.TextureAssets.Should().ContainSingle();
        graph.ResourceLinks.Should().BeEmpty(); // No speculative links produced!
    }

    [Fact]
    public void BuildGraph_UnknownResourcesRetainedInOtherResources()
    {
        var unknownEntry = new PackageResourceEntry(new PackageResourceId(0x99999999, 0, 9), 512, 50, 50, PackageCompressionKind.None, 0);
        var pkgRow = PackageResourceRow.FromEntry(unknownEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "sample.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.OtherResources.Should().ContainSingle(r => r.FormattedKey == unknownEntry.Id.FormattedKey);
    }

    private class FakeDecompositionService : ITs3ObjectModelDecompositionService
    {
        public Ts3ObjectModelDecompositionResult ResultToReturn { get; set; } = new Ts3ObjectModelDecompositionResult(
            IsSuccess: true,
            SourcePackagePath: "sample.package",
            ModlResources: new[] { PackageResourceRow.FromEntry(new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 0, 0, 0, PackageCompressionKind.None, 0)) },
            MlodResources: new[] { PackageResourceRow.FromEntry(new PackageResourceEntry(new PackageResourceId(0x01D10F34, 0, 2), 0, 0, 0, PackageCompressionKind.None, 0)) },
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ModelMetadataResults: new[] { new Mesh.Models.Ts3ObjectModelMetadataResult(true, new PackageResourceId(0x01661233, 0, 1), Mesh.Models.Ts3ObjectModelKind.Modl, 1, 1, Array.Empty<Mesh.Models.Ts3ObjectModelLodInfo>(), Array.Empty<Mesh.Models.Ts3ObjectModelGeometryReference>(), Array.Empty<ConversionIssue>()) },
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<Ts3ObjectModelDecompositionResult> DecomposeAsync(string packageFilePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }

        public Ts3ObjectModelDecompositionResult Decompose(PackageInspectionResult packageInspection)
        {
            return ResultToReturn;
        }
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_CarriesDecompositionResultAndSetsDecompositionMetadataAvailableInCONVG003()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var pkgRow = PackageResourceRow.FromEntry(modlEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "onyx_sample.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var decompService = new FakeDecompositionService();

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.IsSourceGraphReady.Should().BeFalse("IsSourceGraphReady MUST remain false without direct TS3 GEOM.");
        graph.ObjectModelDecomposition.Should().NotBeNull();
        graph.ObjectModelDecomposition!.HasDecompositionMetadata.Should().BeTrue();
        graph.Issues.Should().ContainSingle(i => i.Code == "CONVG003");
        graph.Issues[0].Message.Should().Contain("decomposition metadata available");
    }

    [Fact]
    public void BuildGraph_WhenDecompositionMetadataFails_DoesNotIncludeDecompositionMetadataAvailableInCONVG003()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var pkgRow = PackageResourceRow.FromEntry(modlEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "corrupt_sample.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: false,
                SourcePackagePath: "corrupt_sample.package",
                ModlResources: new[] { pkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { Mesh.Models.Ts3ObjectModelMetadataResult.Failure(modlEntry.Id, Mesh.Models.Ts3ObjectModelKind.Modl, "MODL004", "Compressed payload detected") },
                Issues: new[] { new ConversionIssue("MODL004", "Compressed payload detected", ConversionIssueSeverity.Error) }
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.IsSourceGraphReady.Should().BeFalse();
        graph.ObjectModelDecomposition.Should().NotBeNull();
        graph.ObjectModelDecomposition!.HasDecompositionMetadata.Should().BeFalse();
        graph.Issues.Should().ContainSingle(i => i.Code == "CONVG003");
        graph.Issues[0].Message.Should().NotContain("decomposition metadata available");
    }

    [Fact]
    public async Task BuildGraphAsync_WhenDecompositionMetadataFails_DoesNotIncludeDecompositionMetadataAvailableInCONVG003()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var pkgRow = PackageResourceRow.FromEntry(modlEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "corrupt_sample.package", Header: null,
                Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: false,
                SourcePackagePath: "corrupt_sample.package",
                ModlResources: new[] { pkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { Mesh.Models.Ts3ObjectModelMetadataResult.Failure(modlEntry.Id, Mesh.Models.Ts3ObjectModelKind.Modl, "DECOMP002", "Payload extraction failed") },
                Issues: new[] { new ConversionIssue("DECOMP002", "Payload extraction failed", ConversionIssueSeverity.Error) }
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = await builder.BuildGraphAsync("corrupt_sample.package");

        graph.IsSourceGraphReady.Should().BeFalse();
        graph.ObjectModelDecomposition.Should().NotBeNull();
        graph.ObjectModelDecomposition!.HasDecompositionMetadata.Should().BeFalse();
        graph.Issues.Should().ContainSingle(i => i.Code == "CONVG003");
        graph.Issues[0].Message.Should().NotContain("decomposition metadata available");
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_ResolvesGeomCandidates_AndSetsIsSourceGraphReadyTrue()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var geomEntry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);

        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);
        var geomPkgRow = PackageResourceRow.FromEntry(geomEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "resolved_sample.package", Header: null,
                Resources: new[] { modlPkgRow, geomPkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var geomClassification = new MeshResourceClassification(
            geomEntry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );
        var geomMeshRow = new MeshResourceRow(
            FormattedKey: geomEntry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            DataOffset: 200, CompressedSize: 50, DecompressedSize: 100, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 50, FaceCount: 20, BoneCount: 1, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: geomEntry, Classification: geomClassification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(true, "resolved_sample.package", new[] { geomMeshRow }, Array.Empty<ConversionIssue>())
        };

        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: new[] { new Mesh.Models.Ts3ObjectModelLodInfo(0, 1, modlEntry.Id) },
            GeometryReferences: new[] { new Mesh.Models.Ts3ObjectModelGeometryReference(geomEntry.Id, 0, "GeometryRef") },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "resolved_sample.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.HasImportableMesh.Should().BeTrue();
        graph.IsSourceGraphReady.Should().BeTrue();
        graph.MeshAssets.Should().HaveCount(1);
        graph.MeshAssets[0].FormattedKey.Should().Be(geomEntry.Id.FormattedKey);
        graph.Issues.Should().NotContain(i => i.Code == "CONVG003");
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_WhenGeomMissingInPackage_EmitsCONVG004_AndRejectsCandidate()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var missingGeomId = new PackageResourceId(0x015A1849, 0, 99);

        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "missing_geom.package", Header: null,
                Resources: new[] { modlPkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: Array.Empty<Mesh.Models.Ts3ObjectModelLodInfo>(),
            GeometryReferences: new[] { new Mesh.Models.Ts3ObjectModelGeometryReference(missingGeomId, 0, "MissingRef") },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "missing_geom.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.HasImportableMesh.Should().BeFalse();
        graph.IsSourceGraphReady.Should().BeFalse();
        graph.MeshAssets.Should().BeEmpty();
        graph.Issues.Should().Contain(i => i.Code == "CONVG004");
        graph.Issues.Should().Contain(i => i.Code == "CONVG003");
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_WhenGeomImportFails_EmitsCONVG005_AndRejectsCandidate()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var corruptGeomEntry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);

        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);
        var geomPkgRow = PackageResourceRow.FromEntry(corruptGeomEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "corrupt_geom.package", Header: null,
                Resources: new[] { modlPkgRow, geomPkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var geomClassification = new MeshResourceClassification(
            corruptGeomEntry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );
        var corruptMeshRow = new MeshResourceRow(
            FormattedKey: corruptGeomEntry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            DataOffset: 200, CompressedSize: 50, DecompressedSize: 100, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: false,
            VertexCount: null, FaceCount: null, BoneCount: null, HasNormals: false, HasUv0: false, HasBoneWeights: false, ValidationIssueCount: 1,
            Issues: new[] { new ConversionIssue("GEOM001", "Truncated GEOM payload", ConversionIssueSeverity.Error) }, Entry: corruptGeomEntry, Classification: geomClassification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(true, "corrupt_geom.package", new[] { corruptMeshRow }, Array.Empty<ConversionIssue>())
        };
        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: Array.Empty<Mesh.Models.Ts3ObjectModelLodInfo>(),
            GeometryReferences: new[] { new Mesh.Models.Ts3ObjectModelGeometryReference(corruptGeomEntry.Id, 0, "CorruptRef") },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "corrupt_geom.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.HasImportableMesh.Should().BeFalse();
        graph.IsSourceGraphReady.Should().BeFalse();
        graph.MeshAssets.Should().BeEmpty();
        graph.Issues.Should().Contain(i => i.Code == "CONVG005");
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_WhenModelReferenceIsCyclic_EmitsCONVG006()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "cyclic.package", Header: null,
                Resources: new[] { modlPkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: new[] { new Mesh.Models.Ts3ObjectModelLodInfo(0, 1, modlEntry.Id) },
            GeometryReferences: new[] { new Mesh.Models.Ts3ObjectModelGeometryReference(modlEntry.Id, 0, "SelfRef") },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "cyclic.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.Issues.Should().Contain(i => i.Code == "CONVG006");
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_SortsCandidatesDeterministically()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var geomEntryB = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);
        var geomEntryA = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 1), 150, 50, 100, PackageCompressionKind.Zlib, 0);

        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);
        var geomPkgRowB = PackageResourceRow.FromEntry(geomEntryB);
        var geomPkgRowA = PackageResourceRow.FromEntry(geomEntryA);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "sorted.package", Header: null,
                Resources: new[] { modlPkgRow, geomPkgRowB, geomPkgRowA }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var geomClassB = new MeshResourceClassification(geomEntryB.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var geomClassA = new MeshResourceClassification(geomEntryA.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>());

        var meshRowB = new MeshResourceRow(
            FormattedKey: geomEntryB.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            DataOffset: 200, CompressedSize: 50, DecompressedSize: 100, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 50, FaceCount: 20, BoneCount: 1, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: geomEntryB, Classification: geomClassB
        );

        var meshRowA = new MeshResourceRow(
            FormattedKey: geomEntryA.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000001",
            DataOffset: 150, CompressedSize: 50, DecompressedSize: 100, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 30, FaceCount: 10, BoneCount: 1, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: geomEntryA, Classification: geomClassA
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(true, "sorted.package", new[] { meshRowB, meshRowA }, Array.Empty<ConversionIssue>())
        };
        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: Array.Empty<Mesh.Models.Ts3ObjectModelLodInfo>(),
            GeometryReferences: new[]
            {
                new Mesh.Models.Ts3ObjectModelGeometryReference(geomEntryB.Id, 0, "RefB"),
                new Mesh.Models.Ts3ObjectModelGeometryReference(geomEntryA.Id, 0, "RefA")
            },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "sorted.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.MeshAssets.Should().HaveCount(2);
        string.Compare(graph.MeshAssets[0].FormattedKey, graph.MeshAssets[1].FormattedKey, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_WhenGeomIsTs4Geom_EmitsCONVG005_AndRejectsCandidate()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var ts4GeomEntry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);

        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);
        var geomPkgRow = PackageResourceRow.FromEntry(ts4GeomEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "ts4_geom.package", Header: null,
                Resources: new[] { modlPkgRow, geomPkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var ts4Classification = new MeshResourceClassification(
            ts4GeomEntry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS4 CAS GEOM", GameVersion.Sims4, Array.Empty<ConversionIssue>()
        );
        var ts4MeshRow = new MeshResourceRow(
            FormattedKey: ts4GeomEntry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            DataOffset: 200, CompressedSize: 50, DecompressedSize: 100, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS4 CAS GEOM",
            DetectedGameVersion: GameVersion.Sims4, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 50, FaceCount: 20, BoneCount: 1, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: ts4GeomEntry, Classification: ts4Classification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(true, "ts4_geom.package", new[] { ts4MeshRow }, Array.Empty<ConversionIssue>())
        };
        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: Array.Empty<Mesh.Models.Ts3ObjectModelLodInfo>(),
            GeometryReferences: new[] { new Mesh.Models.Ts3ObjectModelGeometryReference(ts4GeomEntry.Id, 0, "Ts4Ref") },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "ts4_geom.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.HasImportableMesh.Should().BeTrue();
        graph.IsSourceGraphReady.Should().BeTrue();
        graph.MeshAssets.Should().HaveCount(1);
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_WhenGeomIsUnknownMesh_EmitsCONVG005_AndRejectsCandidate()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var unknownGeomEntry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);

        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);
        var geomPkgRow = PackageResourceRow.FromEntry(unknownGeomEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "unknown_mesh.package", Header: null,
                Resources: new[] { modlPkgRow, geomPkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var unknownClassification = new MeshResourceClassification(
            unknownGeomEntry.Id, MeshClassificationKind.Unknown, MeshRoleKind.Unknown, "Unknown", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );
        var unknownMeshRow = new MeshResourceRow(
            FormattedKey: unknownGeomEntry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            DataOffset: 200, CompressedSize: 50, DecompressedSize: 100, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.Unknown, RoleKind: MeshRoleKind.Unknown, FormatName: "Unknown",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: false, CanInspectCanonicalMesh: false,
            VertexCount: null, FaceCount: null, BoneCount: null, HasNormals: false, HasUv0: false, HasBoneWeights: false, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: unknownGeomEntry, Classification: unknownClassification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(true, "unknown_mesh.package", new[] { unknownMeshRow }, Array.Empty<ConversionIssue>())
        };
        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: Array.Empty<Mesh.Models.Ts3ObjectModelLodInfo>(),
            GeometryReferences: new[] { new Mesh.Models.Ts3ObjectModelGeometryReference(unknownGeomEntry.Id, 0, "UnknownRef") },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "unknown_mesh.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.HasImportableMesh.Should().BeFalse();
        graph.IsSourceGraphReady.Should().BeFalse();
        graph.MeshAssets.Should().BeEmpty();
        graph.Issues.Should().Contain(i => i.Code == "CONVG005");
    }

    [Fact]
    public void BuildGraph_WithDecompositionService_WhenGeomHasWrongRole_EmitsCONVG005_AndRejectsCandidate()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var wrongRoleGeomEntry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);

        var modlPkgRow = PackageResourceRow.FromEntry(modlEntry);
        var geomPkgRow = PackageResourceRow.FromEntry(wrongRoleGeomEntry);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(
                IsSuccess: true, FilePath: "wrong_role.package", Header: null,
                Resources: new[] { modlPkgRow, geomPkgRow }, Issues: Array.Empty<ConversionIssue>()
            )
        };

        var wrongRoleClassification = new MeshResourceClassification(
            wrongRoleGeomEntry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Rig, "TS3 GEOM Rig", GameVersion.Sims3, Array.Empty<ConversionIssue>()
        );
        var wrongRoleMeshRow = new MeshResourceRow(
            FormattedKey: wrongRoleGeomEntry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000002",
            DataOffset: 200, CompressedSize: 50, DecompressedSize: 100, CompressionName: "Zlib",
            ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Rig, FormatName: "TS3 GEOM Rig",
            DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
            VertexCount: 50, FaceCount: 20, BoneCount: 1, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
            Issues: Array.Empty<ConversionIssue>(), Entry: wrongRoleGeomEntry, Classification: wrongRoleClassification
        );

        var meshService = new FakeMeshInspectionService
        {
            ResultToReturn = new MeshInspectionResult(true, "wrong_role.package", new[] { wrongRoleMeshRow }, Array.Empty<ConversionIssue>())
        };
        var texService = new FakeTextureInspectionService();

        var metaResult = new Mesh.Models.Ts3ObjectModelMetadataResult(
            IsSuccess: true,
            ResourceId: modlEntry.Id,
            ModelKind: Mesh.Models.Ts3ObjectModelKind.Modl,
            Version: 1,
            LodCount: 1,
            LodInfos: Array.Empty<Mesh.Models.Ts3ObjectModelLodInfo>(),
            GeometryReferences: new[] { new Mesh.Models.Ts3ObjectModelGeometryReference(wrongRoleGeomEntry.Id, 0, "WrongRoleRef") },
            Issues: Array.Empty<ConversionIssue>()
        );

        var decompService = new FakeDecompositionService
        {
            ResultToReturn = new Ts3ObjectModelDecompositionResult(
                IsSuccess: true,
                SourcePackagePath: "wrong_role.package",
                ModlResources: new[] { modlPkgRow },
                MlodResources: Array.Empty<PackageResourceRow>(),
                RigResources: Array.Empty<PackageResourceRow>(),
                RsltResources: Array.Empty<PackageResourceRow>(),
                ModelMetadataResults: new[] { metaResult },
                Issues: Array.Empty<ConversionIssue>()
            )
        };

        var builder = new DecorativeObjectSourceGraphBuilder(pkgService, meshService, texService, decompService);

        var graph = builder.BuildGraph(pkgService.ResultToReturn);

        graph.HasImportableMesh.Should().BeFalse();
        graph.IsSourceGraphReady.Should().BeFalse();
        graph.MeshAssets.Should().BeEmpty();
        graph.Issues.Should().Contain(i => i.Code == "CONVG005");
    }
}
