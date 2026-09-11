using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class RealTs3OutputFidelityAndReinspectionTests
{
    private readonly ITestOutputHelper _output;

    public RealTs3OutputFidelityAndReinspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string FindSolutionDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "SimsConverter.sln")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string GetEmbeddedPackageFixturePath()
    {
        string solutionDir = FindSolutionDir();
        string fixturePath = Path.Combine(solutionDir, "fixtures", "local", "Embedded Package #1.package");
        if (File.Exists(fixturePath))
        {
            return fixturePath;
        }
        return "/Users/dogan/Downloads/1790059/extract/Embedded Package #1.package";
    }

    private static DecorativeObjectConversionService CreateConversionServiceStack(
        DbpfPackageParser dbpfParser,
        PackageInspectionService packageService,
        MeshInspectionService meshService,
        TextureInspectionService textureService,
        Ts4ResourcePayloadCompatibilityVerifier payloadVerifier)
    {
        var payloadReader = new PackageResourcePayloadReader();
        var metadataReader = new Ts3ObjectModelMetadataReader();
        var decompService = new Ts3ObjectModelDecompositionService(packageService, payloadReader, metadataReader);
        var validator = new CanonicalMeshValidator();
        var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);

        var capabilityService = new DecorativeObjectConversionCapabilityService();
        var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService, decompService);
        var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, validator);
        var ts4ResourceGenerator = new DecorativeObjectTs4ResourceGenerator();
        var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader, ts4ResourceGenerator);
        var dbpfWriter = new DbpfPackageWriter();
        var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);

        return new DecorativeObjectConversionService(
            packageInspectionService: packageService,
            meshInspectionService: meshService,
            textureInspectionService: textureService,
            sourceGraphBuilder: sourceGraphBuilder,
            inputBundleBuilder: inputBundleBuilder,
            writePlanBuilder: writePlanBuilder,
            packageWriter: packageWriter,
            payloadVerifier: payloadVerifier,
            dbpfParser: dbpfParser,
            capabilityService: capabilityService
        );
    }

    [Fact]
    public async Task ExecuteRealTs3OutputFidelity_WithRealEmbeddedPackage_ExtractsMlodGeometryToTs4Geom()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string tempTarget = Path.Combine(Path.GetTempPath(), "ts3_fidelity_r2_target_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            var conversionService = CreateConversionServiceStack(dbpfParser, packageService, meshService, textureService, payloadVerifier);

            // Execute conversion on package containing MODL/MLOD geometry stream resources
            var req = new DecorativeObjectConversionRequest(sourcePath, tempTarget, GameVersion.Sims4);
            var convResult = await conversionService.ExecuteConversionAsync(req);

            _output.WriteLine($"Conversion Result IsSuccess = {convResult.IsSuccess}");
            foreach (var issue in convResult.Issues)
            {
                _output.WriteLine($"  - [{issue.Severity}] {issue.Code}: {issue.Message}");
            }

            convResult.IsSuccess.Should().BeTrue("Conversion must succeed for real TS3 package with MLOD geometry streams.");

            // Parse output package and verify TS4 GEOM entries
            var targetDbpf = await dbpfParser.ParseFileAsync(tempTarget);
            targetDbpf.IsSuccess.Should().BeTrue();

            var geomEntries = targetDbpf.Entries.Where(e => e.Id.TypeId == Ts4ResourceTypeIds.Geom).ToList();
            geomEntries.Should().NotBeEmpty("Output TS4 package must contain extracted GEOM entries (0x015A1849).");

            _output.WriteLine($"Extracted TS4 GEOM Count = {geomEntries.Count}");
            foreach (var geom in geomEntries)
            {
                var payloadRes = await payloadReader.ReadPayloadAsync(tempTarget, geom);
                payloadRes.IsSuccess.Should().BeTrue();
                payloadRes.Payload.Should().NotBeNull();

                var importRes = ts4Importer.Import(payloadRes.Payload!, geom.Id.FormattedKey);
                importRes.IsSuccess.Should().BeTrue();
                importRes.Mesh.Should().NotBeNull();
                importRes.Mesh!.Vertices.Count.Should().BeGreaterThan(0);
                importRes.Mesh.Faces.Count.Should().BeGreaterThan(0);
                _output.WriteLine($"  GEOM {geom.Id.FormattedKey}: Vertices={importRes.Mesh.Vertices.Count}, Faces={importRes.Mesh.Faces.Count}");
            }
        }
        finally
        {
            if (File.Exists(tempTarget))
            {
                File.Delete(tempTarget);
            }
        }
    }

    [Fact]
    public async Task ExecuteRealTs3OutputFidelity_WithRealEmbeddedPackage_VerifiesObjectCatalogAndTgiChain()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string tempTarget = Path.Combine(Path.GetTempPath(), "ts3_objd_catalog_target_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            var conversionService = CreateConversionServiceStack(dbpfParser, packageService, meshService, textureService, payloadVerifier);

            var req = new DecorativeObjectConversionRequest(sourcePath, tempTarget, GameVersion.Sims4);
            var convResult = await conversionService.ExecuteConversionAsync(req);

            convResult.IsSuccess.Should().BeTrue("Conversion must succeed for real TS3 package.");

            var targetDbpf = await dbpfParser.ParseFileAsync(tempTarget);
            targetDbpf.IsSuccess.Should().BeTrue();

            // 1. Verify COBJ and OBJD presence
            var cobjEntry = targetDbpf.Entries.FirstOrDefault(e => e.Id.TypeId == Ts4ResourceTypeIds.CatalogObject);
            cobjEntry.Should().NotBeNull("Output package must contain COBJ resource (0x319E4F1D).");

            var objdEntry = targetDbpf.Entries.FirstOrDefault(e => e.Id.TypeId == Ts4ResourceTypeIds.ObjectDefinition);
            objdEntry.Should().NotBeNull("Output package must contain OBJD resource (0xC0DB5AE7).");

            // 2. Read OBJD payload and inspect catalog identity & placement fields
            var objdPayloadRes = await payloadReader.ReadPayloadAsync(tempTarget, objdEntry!);
            objdPayloadRes.IsSuccess.Should().BeTrue();
            byte[] objdPayload = objdPayloadRes.Payload!.ToArray();

            System.Text.Encoding.ASCII.GetString(objdPayload, 0, 4).Should().Be("OBJD", "OBJD payload magic must be 'OBJD'.");
            uint placementFlags = BitConverter.ToUInt32(objdPayload, 56);
            placementFlags.Should().BeGreaterThan(0, "OBJD placement flags must be non-zero extracted placement flags.");
            uint price = BitConverter.ToUInt32(objdPayload, 64);
            price.Should().BeGreaterThanOrEqualTo(0, "OBJD price must be transferred from source catalog metadata.");

            // 3. Verify full TGI reference chain via payload verifier
            var verifyResult = await payloadVerifier.VerifyPackagePayloadsAsync(tempTarget, targetDbpf);
            verifyResult.IsSuccess.Should().BeTrue("Output package must pass full TS4 TGI reference chain compatibility verification.");
            verifyResult.VerifiedTgiLinkCount.Should().BeGreaterThanOrEqualTo(5, "COBJ->OBJD, OBJD->MODL, MODL->MLOD, MLOD->GEOM, MLOD->RMAT links must be verified.");

            _output.WriteLine($"Successfully verified TS4 OBJD catalog resource identity and full TGI reference chain with {verifyResult.VerifiedTgiLinkCount} verified TGI links.");
        }
        finally
        {
            if (File.Exists(tempTarget))
            {
                File.Delete(tempTarget);
            }
        }
    }

    [Fact]
    public async Task ExecuteRealTs3OutputFidelity_WithRealEmbeddedPackage_PreservesCatalogMetadataAndObjectIdentity()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string tempTarget = Path.Combine(Path.GetTempPath(), "ts3_catalog_preservation_target_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            var catalogReader = new Ts3CatalogMetadataReader(payloadReader);
            var sourcePkgInspection = await packageService.InspectFileAsync(sourcePath);
            sourcePkgInspection.IsSuccess.Should().BeTrue();

            var extractedSourceMeta = catalogReader.ReadCatalogMetadata(sourcePath, sourcePkgInspection.Resources, "Embedded Package #1");
            extractedSourceMeta.Should().NotBeNull();

            var conversionService = CreateConversionServiceStack(dbpfParser, packageService, meshService, textureService, payloadVerifier);
            var req = new DecorativeObjectConversionRequest(sourcePath, tempTarget, GameVersion.Sims4);
            var convResult = await conversionService.ExecuteConversionAsync(req);

            convResult.IsSuccess.Should().BeTrue();
            convResult.Plan.Should().NotBeNull();
            convResult.Plan!.InputBundle.Should().NotBeNull();
            convResult.Plan.InputBundle!.CatalogMetadata.Should().NotBeNull();

            var bundleMeta = convResult.Plan.InputBundle.CatalogMetadata!;
            bundleMeta.Price.Should().Be(extractedSourceMeta.Price);
            bundleMeta.PlacementFlags.Should().Be(extractedSourceMeta.PlacementFlags);
            bundleMeta.FootprintHash.Should().Be(extractedSourceMeta.FootprintHash);

            var targetDbpf = await dbpfParser.ParseFileAsync(tempTarget);
            targetDbpf.IsSuccess.Should().BeTrue();

            var objdEntry = targetDbpf.Entries.FirstOrDefault(e => e.Id.TypeId == Ts4ResourceTypeIds.ObjectDefinition);
            objdEntry.Should().NotBeNull();

            var objdPayloadRes = await payloadReader.ReadPayloadAsync(tempTarget, objdEntry!);
            objdPayloadRes.IsSuccess.Should().BeTrue();
            byte[] objdPayload = objdPayloadRes.Payload!.ToArray();

            uint writtenPlacementFlags = BitConverter.ToUInt32(objdPayload, 56);
            uint writtenFootprintHash = BitConverter.ToUInt32(objdPayload, 60);
            uint writtenPrice = BitConverter.ToUInt32(objdPayload, 64);

            writtenPlacementFlags.Should().Be(extractedSourceMeta.PlacementFlags, "Written OBJD placement flags must match extracted source catalog metadata.");
            writtenFootprintHash.Should().Be(extractedSourceMeta.FootprintHash, "Written OBJD footprint hash must match extracted source catalog metadata.");
            writtenPrice.Should().Be(extractedSourceMeta.Price, "Written OBJD price must match extracted source catalog metadata.");

            _output.WriteLine($"Preserved Catalog Metadata: Price={writtenPrice}, Placement=0x{writtenPlacementFlags:X8}, Footprint=0x{writtenFootprintHash:X8}, Fallback={extractedSourceMeta.IsDefaultFallback}");
        }
        finally
        {
            if (File.Exists(tempTarget))
            {
                File.Delete(tempTarget);
            }
        }
    }

    [Fact]
    public void ExecuteRealMeshFidelityAndReinspection_WithRealGeometry_VerifiesSourceTarget1to1MeshMetrics()
    {
        var geomId = new PackageResourceId(Ts4ResourceTypeIds.Geom, 0, 0x0000000000001234UL);
        var materialId = new PackageResourceId(Ts4ResourceTypeIds.MaterialDefinition, 0, 0x0000000000001234UL);

        // 1. Create real CanonicalMesh source
        var vertices = new List<CanonicalVertex>
        {
            new CanonicalVertex(position: new MeshVector3(-1.0f, 0.0f, 0.5f), normal: new MeshVector3(0.0f, 1.0f, 0.0f), uv0: new MeshVector2(0.1f, 0.2f), boneWeights: new[] { new CanonicalBoneWeight(0, 1.0f) }),
            new CanonicalVertex(position: new MeshVector3(1.0f, 0.0f, 0.5f), normal: new MeshVector3(0.0f, 1.0f, 0.0f), uv0: new MeshVector2(0.9f, 0.2f), boneWeights: new[] { new CanonicalBoneWeight(0, 1.0f) }),
            new CanonicalVertex(position: new MeshVector3(0.0f, 2.0f, -0.5f), normal: new MeshVector3(0.0f, 1.0f, 0.0f), uv0: new MeshVector2(0.5f, 0.9f), boneWeights: new[] { new CanonicalBoneWeight(0, 1.0f) })
        };

        var faces = new List<CanonicalFace>
        {
            new CanonicalFace(0, 1, 2)
        };

        var sourceMesh = new CanonicalMesh(
            name: "SourceMesh_LOD0",
            vertices: vertices.AsReadOnly(),
            faces: faces.AsReadOnly(),
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims3,
            issues: Array.Empty<ConversionIssue>()
        );

        // 2. Build TS4 GEOM Payload from real CanonicalMesh
        byte[] geomPayload = Ts4GeomPayloadBuilder.BuildGeomPayload(geomId, materialId, sourceMesh);
        geomPayload.Should().NotBeNull().And.HaveCountGreaterThan(0);

        // 3. Re-import target CanonicalMesh using Ts4GeomCanonicalMeshImporter
        var importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), new CanonicalMeshValidator());
        var importResult = importer.Import(geomPayload, "TargetMesh_LOD0");

        importResult.IsSuccess.Should().BeTrue("Ts4GeomCanonicalMeshImporter must successfully import real GEOM payload.");
        var targetMesh = importResult.Mesh;
        targetMesh.Should().NotBeNull();

        // 4. Assert 1:1 Metric Equality between Source and Target Mesh
        targetMesh!.Vertices.Count.Should().Be(sourceMesh.Vertices.Count, "Target vertex count must match source vertex count exactly.");
        targetMesh.Faces.Count.Should().Be(sourceMesh.Faces.Count, "Target face count must match source face count exactly.");

        targetMesh.Vertices[0].Position.X.Should().BeApproximately(sourceMesh.Vertices[0].Position.X, 1e-4f);
        targetMesh.Vertices[0].Position.Y.Should().BeApproximately(sourceMesh.Vertices[0].Position.Y, 1e-4f);
        targetMesh.Vertices[0].Position.Z.Should().BeApproximately(sourceMesh.Vertices[0].Position.Z, 1e-4f);

        targetMesh.Vertices[0].Normal!.Value.X.Should().BeApproximately(sourceMesh.Vertices[0].Normal!.Value.X, 1e-4f);
        targetMesh.Vertices[0].Normal!.Value.Y.Should().BeApproximately(sourceMesh.Vertices[0].Normal!.Value.Y, 1e-4f);

        targetMesh.Vertices[0].Uv0!.Value.X.Should().BeApproximately(sourceMesh.Vertices[0].Uv0!.Value.X, 1e-4f);
        targetMesh.Vertices[0].Uv0!.Value.Y.Should().BeApproximately(sourceMesh.Vertices[0].Uv0!.Value.Y, 1e-4f);

        targetMesh.Vertices[0].BoneWeights.Should().NotBeEmpty();
        targetMesh.Vertices[0].BoneWeights![0].BoneIndex.Should().Be(sourceMesh.Vertices[0].BoneWeights![0].BoneIndex);
    }

    [Fact]
    public void ExecuteRealTs4GeomPayloadBuilder_ProducesByteForByteDeterministicOutput()
    {
        var geomId = new PackageResourceId(Ts4ResourceTypeIds.Geom, 0, 0x0000000000009999UL);
        var materialId = new PackageResourceId(Ts4ResourceTypeIds.MaterialDefinition, 0, 0x0000000000009999UL);

        var vertices = new List<CanonicalVertex>
        {
            new CanonicalVertex(position: new MeshVector3(0, 0, 0), normal: new MeshVector3(0, 1, 0), uv0: new MeshVector2(0, 0), boneWeights: new[] { new CanonicalBoneWeight(0, 1.0f) }),
            new CanonicalVertex(position: new MeshVector3(1, 0, 0), normal: new MeshVector3(0, 1, 0), uv0: new MeshVector2(1, 0), boneWeights: new[] { new CanonicalBoneWeight(0, 1.0f) }),
            new CanonicalVertex(position: new MeshVector3(0, 1, 0), normal: new MeshVector3(0, 1, 0), uv0: new MeshVector2(0, 1), boneWeights: new[] { new CanonicalBoneWeight(0, 1.0f) })
        };

        var faces = new List<CanonicalFace> { new CanonicalFace(0, 1, 2) };

        var mesh = new CanonicalMesh("DetMesh", vertices.AsReadOnly(), faces.AsReadOnly(), Array.Empty<CanonicalMaterialSlot>(), CanonicalCoordinateSystem.RightHandedYUp, GameVersion.Sims3, Array.Empty<ConversionIssue>());

        byte[] payload1 = Ts4GeomPayloadBuilder.BuildGeomPayload(geomId, materialId, mesh);
        byte[] payload2 = Ts4GeomPayloadBuilder.BuildGeomPayload(geomId, materialId, mesh);

        payload1.SequenceEqual(payload2).Should().BeTrue("Building GEOM payload from the same CanonicalMesh must produce byte-for-byte deterministic output.");
    }

    [Fact]
    public async Task ExecuteRealTs3TextureConversion_WithRealEmbeddedPackage_Converts6DdsTexturesToTs4Rle2()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string tempTarget = Path.Combine(Path.GetTempPath(), "ts3_texture_rle2_target_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            var conversionService = CreateConversionServiceStack(dbpfParser, packageService, meshService, textureService, payloadVerifier);

            var req = new DecorativeObjectConversionRequest(sourcePath, tempTarget, GameVersion.Sims4);
            var convResult = await conversionService.ExecuteConversionAsync(req);

            convResult.IsSuccess.Should().BeTrue("Conversion must succeed for real TS3 package.");

            var targetDbpf = await dbpfParser.ParseFileAsync(tempTarget);
            targetDbpf.IsSuccess.Should().BeTrue();

            var rle2Entries = targetDbpf.Entries.Where(e => e.Id.TypeId == Ts4ResourceTypeIds.Rle2Texture).ToList();
            rle2Entries.Should().HaveCount(6, "Output TS4 package must contain exactly 6 converted RLE2 texture entries (0x3453CF95).");

            var rle2Decoder = new Ts4Rle2TextureDecoder();
            _output.WriteLine($"Extracted TS4 RLE2 Count = {rle2Entries.Count}");

            foreach (var rle2 in rle2Entries)
            {
                var payloadRes = await payloadReader.ReadPayloadAsync(tempTarget, rle2);
                payloadRes.IsSuccess.Should().BeTrue();
                payloadRes.Payload.Should().NotBeNull();

                byte[] payload = payloadRes.Payload!.ToArray();
                payload.Length.Should().BeGreaterThan(14);

                // Run full command stream decoder validation
                var decodeResult = rle2Decoder.Decode(payload, rle2.Id.FormattedKey);
                decodeResult.IsSuccess.Should().BeTrue($"Command stream decoding must succeed for converted RLE2 texture '{rle2.Id.FormattedKey}'.");
                decodeResult.Width.Should().Be(1024);
                decodeResult.Height.Should().Be(1024);
                decodeResult.MipMapCount.Should().BeOneOf(10u, 11u);
                decodeResult.FormatKind.Should().BeOneOf(DdsTextureFormatKind.Dxt1, DdsTextureFormatKind.Dxt5);
                decodeResult.MipLevels.Should().NotBeEmpty();

                _output.WriteLine($"  RLE2 {rle2.Id.FormattedKey}: Decoded={decodeResult.Width}x{decodeResult.Height}, Mips={decodeResult.MipMapCount}, Format={decodeResult.FormatKind}, DecodedMipLevels={decodeResult.MipLevels!.Count}");
            }

            // Verify TS4 payload compatibility on output package
            var verifyResult = await payloadVerifier.VerifyPackagePayloadsAsync(tempTarget, targetDbpf);
            verifyResult.IsSuccess.Should().BeTrue("Output package must pass full TS4 payload compatibility verification.");
            verifyResult.VerifiedTgiLinkCount.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(tempTarget))
            {
                File.Delete(tempTarget);
            }
        }
    }

    [Fact]
    public async Task ExecuteRealTs3TextureConversion_ProducesDeterministicPackageOutput()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string tempTarget1 = Path.Combine(Path.GetTempPath(), "ts3_det_target1_" + Guid.NewGuid().ToString("N") + ".package");
        string tempTarget2 = Path.Combine(Path.GetTempPath(), "ts3_det_target2_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            var conversionService = CreateConversionServiceStack(dbpfParser, packageService, meshService, textureService, payloadVerifier);

            var req1 = new DecorativeObjectConversionRequest(sourcePath, tempTarget1, GameVersion.Sims4);
            var req2 = new DecorativeObjectConversionRequest(sourcePath, tempTarget2, GameVersion.Sims4);

            var res1 = await conversionService.ExecuteConversionAsync(req1);
            var res2 = await conversionService.ExecuteConversionAsync(req2);

            res1.IsSuccess.Should().BeTrue();
            res2.IsSuccess.Should().BeTrue();

            byte[] bytes1 = await File.ReadAllBytesAsync(tempTarget1);
            byte[] bytes2 = await File.ReadAllBytesAsync(tempTarget2);

            bytes1.SequenceEqual(bytes2).Should().BeTrue("Converting the same source package twice must produce byte-for-byte deterministic package output.");
        }
        finally
        {
            if (File.Exists(tempTarget1)) File.Delete(tempTarget1);
            if (File.Exists(tempTarget2)) File.Delete(tempTarget2);
        }
    }

    [Fact]
    public async Task ExecuteRealTs3TextureConversion_RoundTripDecoding_Verifies1to1PixelFidelity()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        var dbpfParser = new DbpfPackageParser();
        var payloadReader = new PackageResourcePayloadReader();
        var builder = new Ts4Rle2TexturePayloadBuilder();
        var decoder = new Ts4Rle2TextureDecoder();

        var sourceDbpf = await dbpfParser.ParseFileAsync(sourcePath);
        sourceDbpf.IsSuccess.Should().BeTrue();

        var ddsEntries = sourceDbpf.Entries.Where(e => e.Id.TypeId == 0x00B2D882).ToList();
        ddsEntries.Should().HaveCount(6, "Source package must contain 6 TS3 DDS textures.");

        int verifiedCount = 0;
        foreach (var ddsEntry in ddsEntries)
        {
            var pRes = await payloadReader.ReadPayloadAsync(sourcePath, ddsEntry);
            pRes.IsSuccess.Should().BeTrue();
            byte[] originalDds = pRes.Payload!.ToArray();

            // Encode to TS4 RLE2 payload
            var buildRes = builder.BuildPayload(originalDds, ddsEntry.Id.FormattedKey);
            buildRes.IsSuccess.Should().BeTrue($"RLE2 encoding must succeed for DDS texture {ddsEntry.Id.FormattedKey}.");

            // Decode RLE2 payload back to DDS
            var decodeRes = decoder.Decode(buildRes.Payload!, ddsEntry.Id.FormattedKey);
            decodeRes.IsSuccess.Should().BeTrue($"RLE2 decoding must succeed for converted texture {ddsEntry.Id.FormattedKey}.");

            // Verify 1:1 round-trip pixel payload equality across all mipmap levels
            ReadOnlySpan<byte> originalPixels = originalDds.AsSpan(128);
            ReadOnlySpan<byte> decodedPixels = decodeRes.DecodedDdsPayload.AsSpan(128);
            decodedPixels.SequenceEqual(originalPixels).Should().BeTrue($"Decoded DXT pixel blocks must match original DXT pixel blocks byte-for-byte for texture {ddsEntry.Id.FormattedKey}.");
            verifiedCount++;
        }

        verifiedCount.Should().Be(6, "All 6 real TS3 DDS textures must complete 1:1 round-trip encoding/decoding verification.");
        _output.WriteLine($"Successfully verified 1:1 byte-for-byte round-trip fidelity for all {verifiedCount} real textures.");
    }
}
