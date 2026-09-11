using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Contracts;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;
using SimsConverter.Package.Contracts;
using Xunit;

namespace SimsConverter.Application.Tests;

public class DecorativeObjectConversionInputBundleBuilderTests
{
    private class FakePayloadReader : IPackageResourcePayloadReader
    {
        public PackageResourceEntry? LastReadEntry { get; private set; }

        public PackageResourcePayloadResult ResultToReturn { get; set; } = new PackageResourcePayloadResult(
            IsSuccess: true,
            Payload: new byte[] { 0x47, 0x45, 0x4F, 0x4D }, // "GEOM"
            Issues: Array.Empty<ConversionIssue>()
        );

        public PackageResourcePayloadResult ReadPayload(string packageFilePath, PackageResourceEntry entry)
        {
            LastReadEntry = entry;
            return ResultToReturn;
        }

        public Task<PackageResourcePayloadResult> ReadPayloadAsync(string packageFilePath, PackageResourceEntry entry, System.Threading.CancellationToken cancellationToken = default)
        {
            LastReadEntry = entry;
            return Task.FromResult(ResultToReturn);
        }
    }

    private class FakeGeomImporter : ITs3GeomCanonicalMeshImporter
    {
        public Ts3GeomImportResult ResultToReturn { get; set; }

        public FakeGeomImporter()
        {
            var vertices = new List<CanonicalVertex>
            {
                new CanonicalVertex(new MeshVector3(0, 0, 0), new MeshVector3(0, 1, 0), null, new MeshVector2(0, 0), null, null)
            };
            var face = new CanonicalFace(0, 0, 0);
            var mesh = new CanonicalMesh("TestMesh", vertices.AsReadOnly(), new[] { face }, null, CanonicalCoordinateSystem.RightHandedYUp, GameVersion.Sims3, null);
            ResultToReturn = new Ts3GeomImportResult(true, mesh, Array.Empty<ConversionIssue>());
        }

        public Ts3GeomImportResult Import(ReadOnlySpan<byte> buffer, string? nameHint = null) => ResultToReturn;
        public Ts3GeomImportResult Import(Stream stream, string? nameHint = null) => ResultToReturn;
        public Task<Ts3GeomImportResult> ImportAsync(Stream stream, string? nameHint = null, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
    }

    private class FakeMeshValidator : ICanonicalMeshValidator
    {
        public CanonicalMeshValidationResult ResultToReturn { get; set; } = new CanonicalMeshValidationResult(true, Array.Empty<ConversionIssue>());

        public CanonicalMeshValidationResult Validate(CanonicalMesh mesh) => ResultToReturn;
    }

    [Fact]
    public void BuildBundle_NullSourceGraph_ReturnsInvalidBundleWithCONVB000()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var bundle = builder.BuildBundle(null!, "output.package");

        bundle.IsBundleValid.Should().BeFalse();
        bundle.Issues.Should().Contain(i => i.Code == "CONVB000");
    }

    [Fact]
    public void BuildBundle_UnreadySourceGraph_ReturnsInvalidBundleWithCONVB000()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var unreadyGraph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "unready.package",
            TotalResourceCount: 1,
            MeshAssets: Array.Empty<DecorativeObjectSourceMeshAsset>(),
            TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: false,
            HasTextureCandidates: false,
            IsSourceGraphReady: false,
            Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(unreadyGraph, "output.package");

        bundle.IsBundleValid.Should().BeFalse();
        bundle.Issues.Should().Contain(i => i.Code == "CONVB000");
    }

    private static PackageResourceEntry CreateEntry(PackageResourceId id) =>
        new PackageResourceEntry(id, DataOffset: 1024, CompressedSize: 256, DecompressedSize: 512, CompressionKind: PackageCompressionKind.None, CompressionFlags: 0);

    [Fact]
    public void BuildBundle_ValidSourceGraph_CreatesValidConversionInputBundle()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var geomId = new PackageResourceId(0x015A1849, 0, 1);
        var meshAsset = new DecorativeObjectSourceMeshAsset(
            FormattedKey: geomId.FormattedKey,
            ResourceId: geomId,
            ClassificationKind: MeshClassificationKind.KnownMesh,
            RoleKind: MeshRoleKind.Geometry,
            FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3,
            CanInspectCanonicalMesh: true,
            VertexCount: 1,
            FaceCount: 1,
            BoneCount: 0,
            Issues: Array.Empty<ConversionIssue>(),
            Entry: CreateEntry(geomId)
        );

        var texAsset = new DecorativeObjectSourceTextureAsset(
            FormattedKey: "0x00B2D882:0x00000000:0x0000000000000001",
            ResourceId: new PackageResourceId(0x00B2D882, 0, 1),
            ClassificationKind: TextureClassificationKind.KnownTexture,
            MapKind: TextureMapKind.Diffuse,
            FormatName: "TS3 DDS",
            CanExtractRawPayload: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var graph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "sample.package",
            TotalResourceCount: 2,
            MeshAssets: new[] { meshAsset },
            TextureAssets: new[] { texAsset },
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: true,
            HasTextureCandidates: true,
            IsSourceGraphReady: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(graph, "target.package");

        bundle.IsBundleValid.Should().BeTrue();
        bundle.SourcePackagePath.Should().Be("sample.package");
        bundle.TargetOutputPath.Should().Be("target.package");
        bundle.MeshBundles.Should().HaveCount(1);
        bundle.MeshBundles[0].CanonicalMesh.Should().NotBeNull();
        bundle.TextureAssets.Should().HaveCount(1);
        bundle.ResourceLinks.Should().BeEmpty("Zero speculative linking guaranteed.");
    }

    [Fact]
    public void BuildBundle_WhenMeshValidationFails_SetsIsBundleValidFalse_AndEmitsCONVB001()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator
        {
            ResultToReturn = new CanonicalMeshValidationResult(false, new[]
            {
                new ConversionIssue("MESH001", "Mesh has no vertices.", ConversionIssueSeverity.Error)
            })
        };
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var geomId = new PackageResourceId(0x015A1849, 0, 1);
        var meshAsset = new DecorativeObjectSourceMeshAsset(
            FormattedKey: geomId.FormattedKey, ResourceId: geomId, ClassificationKind: MeshClassificationKind.KnownMesh,
            RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM", DetectedGameVersion: GameVersion.Sims3,
            CanInspectCanonicalMesh: true, VertexCount: 0, FaceCount: 0, BoneCount: 0, Issues: Array.Empty<ConversionIssue>(),
            Entry: CreateEntry(geomId)
        );

        var graph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "invalid_mesh.package", TotalResourceCount: 1,
            MeshAssets: new[] { meshAsset }, TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(), OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: true, HasTextureCandidates: false, IsSourceGraphReady: true, Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(graph, "target.package");

        bundle.IsBundleValid.Should().BeFalse();
        bundle.Issues.Should().Contain(i => i.Code == "CONVB001");
    }

    [Fact]
    public void BuildBundle_WhenNoTextureCandidates_EmitsCONVB002Warning()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var geomId = new PackageResourceId(0x015A1849, 0, 1);
        var meshAsset = new DecorativeObjectSourceMeshAsset(
            FormattedKey: geomId.FormattedKey, ResourceId: geomId, ClassificationKind: MeshClassificationKind.KnownMesh,
            RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM", DetectedGameVersion: GameVersion.Sims3,
            CanInspectCanonicalMesh: true, VertexCount: 1, FaceCount: 1, BoneCount: 0, Issues: Array.Empty<ConversionIssue>(),
            Entry: CreateEntry(geomId)
        );

        var graph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "no_tex.package", TotalResourceCount: 1,
            MeshAssets: new[] { meshAsset }, TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(), OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: true, HasTextureCandidates: false, IsSourceGraphReady: true, Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(graph, "target.package");

        bundle.Issues.Should().Contain(i => i.Code == "CONVB002");
    }

    [Fact]
    public void BuildBundle_WhenMeshHasWeightsButNoRigResource_EmitsCONVB003Warning()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();

        // Construct mesh with bone weights
        var boneWeight = new CanonicalBoneWeight(0, 1.0f);
        var vertexWithWeight = new CanonicalVertex(new MeshVector3(0, 0, 0), new MeshVector3(0, 1, 0), null, new MeshVector2(0, 0), null, new[] { boneWeight });
        var weightedMesh = new CanonicalMesh("WeightedMesh", new[] { vertexWithWeight }, new[] { new CanonicalFace(0, 0, 0) }, null, CanonicalCoordinateSystem.RightHandedYUp, GameVersion.Sims3, null);

        importer.ResultToReturn = new Ts3GeomImportResult(true, weightedMesh, Array.Empty<ConversionIssue>());

        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var geomId = new PackageResourceId(0x015A1849, 0, 1);
        var meshAsset = new DecorativeObjectSourceMeshAsset(
            FormattedKey: geomId.FormattedKey, ResourceId: geomId, ClassificationKind: MeshClassificationKind.KnownMesh,
            RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM", DetectedGameVersion: GameVersion.Sims3,
            CanInspectCanonicalMesh: true, VertexCount: 1, FaceCount: 1, BoneCount: 1, Issues: Array.Empty<ConversionIssue>(),
            Entry: CreateEntry(geomId)
        );

        var graph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "no_rig.package", TotalResourceCount: 1,
            MeshAssets: new[] { meshAsset }, TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(), OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: true, HasTextureCandidates: false, IsSourceGraphReady: true, Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(graph, "target.package");

        bundle.Issues.Should().Contain(i => i.Code == "CONVB003");
    }

    [Fact]
    public void BuildBundle_WhenMeshAssetEntryIsNull_SetsIsBundleValidFalse_AndEmitsCONVB005()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var geomId = new PackageResourceId(0x015A1849, 0, 1);
        var meshAssetWithNullEntry = new DecorativeObjectSourceMeshAsset(
            FormattedKey: geomId.FormattedKey, ResourceId: geomId, ClassificationKind: MeshClassificationKind.KnownMesh,
            RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM", DetectedGameVersion: GameVersion.Sims3,
            CanInspectCanonicalMesh: true, VertexCount: 1, FaceCount: 1, BoneCount: 0, Issues: Array.Empty<ConversionIssue>(),
            Entry: null
        );

        var graph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "null_entry.package", TotalResourceCount: 1,
            MeshAssets: new[] { meshAssetWithNullEntry }, TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(), OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: true, HasTextureCandidates: false, IsSourceGraphReady: true, Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(graph, "target.package");

        bundle.IsBundleValid.Should().BeFalse();
        bundle.Issues.Should().Contain(i => i.Code == "CONVB005");
    }

    [Fact]
    public void BuildBundle_SortsAllCollectionsDeterministically()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var geomIdB = new PackageResourceId(0x015A1849, 0, 2);
        var geomIdA = new PackageResourceId(0x015A1849, 0, 1);

        var meshAssetB = new DecorativeObjectSourceMeshAsset(geomIdB.FormattedKey, geomIdB, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, true, 1, 1, 0, Array.Empty<ConversionIssue>(), CreateEntry(geomIdB));
        var meshAssetA = new DecorativeObjectSourceMeshAsset(geomIdA.FormattedKey, geomIdA, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, true, 1, 1, 0, Array.Empty<ConversionIssue>(), CreateEntry(geomIdA));

        var texB = new DecorativeObjectSourceTextureAsset("0x00B2D882:0x00000000:0x0000000000000002", new PackageResourceId(0x00B2D882, 0, 2), TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "TS3 DDS", true, Array.Empty<ConversionIssue>());
        var texA = new DecorativeObjectSourceTextureAsset("0x00B2D882:0x00000000:0x0000000000000001", new PackageResourceId(0x00B2D882, 0, 1), TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "TS3 DDS", true, Array.Empty<ConversionIssue>());

        var graph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "sorted.package", TotalResourceCount: 4,
            MeshAssets: new[] { meshAssetB, meshAssetA }, TextureAssets: new[] { texB, texA },
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(), OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: true, HasTextureCandidates: true, IsSourceGraphReady: true, Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(graph, "target.package");

        bundle.MeshBundles.Should().HaveCount(2);
        string.Compare(bundle.MeshBundles[0].FormattedKey, bundle.MeshBundles[1].FormattedKey, StringComparison.Ordinal).Should().BeLessThan(0);
        string.Compare(bundle.TextureAssets[0].FormattedKey, bundle.TextureAssets[1].FormattedKey, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Fact]
    public void BuildBundle_PreservesRealMeshPayloadEntryMetadata_AndStoresRawPayload()
    {
        var payloadReader = new FakePayloadReader();
        var importer = new FakeGeomImporter();
        var validator = new FakeMeshValidator();
        var builder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, importer, validator);

        var geomId = new PackageResourceId(0x015A1849, 0, 1);
        var realEntry = new PackageResourceEntry(geomId, DataOffset: 2048, CompressedSize: 512, DecompressedSize: 1024, CompressionKind: PackageCompressionKind.Zlib, CompressionFlags: 0);
        var meshAsset = new DecorativeObjectSourceMeshAsset(
            FormattedKey: geomId.FormattedKey,
            ResourceId: geomId,
            ClassificationKind: MeshClassificationKind.KnownMesh,
            RoleKind: MeshRoleKind.Geometry,
            FormatName: "TS3 GEOM",
            DetectedGameVersion: GameVersion.Sims3,
            CanInspectCanonicalMesh: true,
            VertexCount: 1,
            FaceCount: 1,
            BoneCount: 0,
            Issues: Array.Empty<ConversionIssue>(),
            Entry: realEntry
        );

        var graph = new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: "real_entry.package",
            TotalResourceCount: 1,
            MeshAssets: new[] { meshAsset },
            TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: true,
            HasTextureCandidates: false,
            IsSourceGraphReady: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var bundle = builder.BuildBundle(graph, "target.package");

        payloadReader.LastReadEntry.Should().NotBeNull();
        payloadReader.LastReadEntry!.DataOffset.Should().Be(2048);
        payloadReader.LastReadEntry.CompressedSize.Should().Be(512);
        payloadReader.LastReadEntry.DecompressedSize.Should().Be(1024);
        payloadReader.LastReadEntry.CompressionKind.Should().Be(PackageCompressionKind.Zlib);

        bundle.MeshBundles.Should().HaveCount(1);
        bundle.MeshBundles[0].RawPayload.Should().NotBeNull();
        bundle.MeshBundles[0].RawPayload.Should().Equal(new byte[] { 0x47, 0x45, 0x4F, 0x4D });
    }
}
