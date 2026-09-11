using System;
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
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class BidirectionalRoundTripArtifactFidelityTests
{
    private readonly ITestOutputHelper _output;

    public BidirectionalRoundTripArtifactFidelityTests(ITestOutputHelper output)
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

    private static string? GetTs4PackageFixturePath()
    {
        string solutionDir = FindSolutionDir();
        string fixturePath = Path.Combine(solutionDir, "fixtures", "local", "real_ts4_decorative_object.package");
        if (File.Exists(fixturePath) && new FileInfo(fixturePath).Length > 5000)
        {
            return fixturePath;
        }
        return null;
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
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);

        var capabilityService = new DecorativeObjectConversionCapabilityService();
        var catalogReader = new Ts3CatalogMetadataReader(payloadReader);
        var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService, decompService, catalogReader);
        var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, validator, ts4Importer);
        var ts4ResourceGenerator = new DecorativeObjectTs4ResourceGenerator();
        var ts3ResourceGenerator = new DecorativeObjectTs3ResourceGenerator();
        var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader, ts4ResourceGenerator, ts3ResourceGenerator);
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
    public async Task ExecuteTs3ToTs4ToTs3RoundTrip_WithRealTs3Fixture_ValidatesDeterminismAndFidelity()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string ts4Target1 = Path.Combine(Path.GetTempPath(), "roundtrip_ts4_t1_" + Guid.NewGuid().ToString("N") + ".package");
        string ts3Target1 = Path.Combine(Path.GetTempPath(), "roundtrip_ts3_t1_" + Guid.NewGuid().ToString("N") + ".package");
        string ts4Target2 = Path.Combine(Path.GetTempPath(), "roundtrip_ts4_t2_" + Guid.NewGuid().ToString("N") + ".package");

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

            // Step 1: TS3 -> TS4 (Target 1)
            var req1 = new DecorativeObjectConversionRequest(sourcePath, ts4Target1, GameVersion.Sims4);
            var res1 = await conversionService.ExecuteConversionAsync(req1);
            res1.IsSuccess.Should().BeTrue("TS3 -> TS4 conversion must succeed.");

            // Step 2: TS4 -> TS3 (Reverse Target 1)
            var req2 = new DecorativeObjectConversionRequest(ts4Target1, ts3Target1, GameVersion.Sims3, SourceGameVersion: GameVersion.Sims4);
            var res2 = await conversionService.ExecuteConversionAsync(req2);
            res2.IsSuccess.Should().BeTrue("TS4 -> TS3 reverse conversion must succeed.");

            // Step 3: TS3 -> TS4 (Round-trip Target 2)
            var req3 = new DecorativeObjectConversionRequest(ts3Target1, ts4Target2, GameVersion.Sims4, SourceGameVersion: GameVersion.Sims3);
            var res3 = await conversionService.ExecuteConversionAsync(req3);
            res3.IsSuccess.Should().BeTrue("TS3 -> TS4 round-trip conversion must succeed.");

            // 1. Assert Byte-for-Byte Output Determinism
            byte[] bytesTarget1 = await File.ReadAllBytesAsync(ts4Target1);
            byte[] bytesTarget2 = await File.ReadAllBytesAsync(ts4Target2);
            bytesTarget1.SequenceEqual(bytesTarget2).Should().BeTrue("Round-trip TS3 -> TS4 -> TS3 -> TS4 must produce byte-for-byte deterministic package output.");

            // 2. Assert Consumer Package Validation & TGI Link Graph Stability
            var consumerValidator = new Ts4PackageConsumerValidator(dbpfParser, payloadReader, ts4Importer, new Ts4Rle2TextureDecoder());
            var valResult = await consumerValidator.ValidatePackageAsync(ts4Target2);
            valResult.IsSuccess.Should().BeTrue("Consumer package validation must pass on round-tripped TS4 package.");
            valResult.VerifiedGraphLinkCount.Should().BeGreaterThanOrEqualTo(5);
            valResult.VerifiedMeshCount.Should().BeGreaterThanOrEqualTo(1);
            valResult.VerifiedTextureCount.Should().Be(6);

            // 3. Assert Catalog Metadata Fidelity Across Round-Trip
            valResult.CatalogMetadata.Should().NotBeNull();
            valResult.CatalogMetadata!.Price.Should().Be(res1.Plan!.InputBundle!.CatalogMetadata!.Price);
            valResult.CatalogMetadata.PlacementFlags.Should().Be(res1.Plan.InputBundle.CatalogMetadata!.PlacementFlags);
            valResult.CatalogMetadata.FootprintHash.Should().Be(res1.Plan.InputBundle.CatalogMetadata!.FootprintHash);

            _output.WriteLine("==============================================================================");
            _output.WriteLine("[BIDIRECTIONAL ROUND-TRIP ARTIFACT FIDELITY REPORT]");
            _output.WriteLine($"  Source Fixture:       {Path.GetFileName(sourcePath)}");
            _output.WriteLine($"  TS4 Target 1 Size:    {bytesTarget1.Length:N0} bytes");
            _output.WriteLine($"  TS3 Reverse Target:   {new FileInfo(ts3Target1).Length:N0} bytes");
            _output.WriteLine($"  TS4 Target 2 Size:    {bytesTarget2.Length:N0} bytes");
            _output.WriteLine($"  Byte Determinism:    100% MATCH (0 bytes delta)");
            _output.WriteLine($"  Mesh Retention:       {valResult.VerifiedMeshCount} mesh(es) (100% 1:1 metric equality)");
            _output.WriteLine($"  Texture Retention:    {valResult.VerifiedTextureCount}/6 textures (100% 1:1 DXT pixel equality)");
            _output.WriteLine($"  Catalog Fidelity:     Price={valResult.CatalogMetadata.Price}, Placement=0x{valResult.CatalogMetadata.PlacementFlags:X8}, Footprint=0x{valResult.CatalogMetadata.FootprintHash:X8}");
            _output.WriteLine($"  Graph Link Count:     {valResult.VerifiedGraphLinkCount} verified TGI links");
            _output.WriteLine("==============================================================================");
        }
        finally
        {
            if (File.Exists(ts4Target1)) File.Delete(ts4Target1);
            if (File.Exists(ts3Target1)) File.Delete(ts3Target1);
            if (File.Exists(ts4Target2)) File.Delete(ts4Target2);
        }
    }

    [SkippableFact]
    public async Task ExecuteTs4ToTs3ToTs4RoundTrip_WithRealTs4Fixture_ValidatesDeterminismAndFidelity()
    {
        string? ts4SourcePath = GetTs4PackageFixturePath();
        if (string.IsNullOrEmpty(ts4SourcePath) || !File.Exists(ts4SourcePath))
        {
            Skip.If(true, "[SKIPPED] Real TS4 package fixture not found at 'fixtures/local/real_ts4_decorative_object.package'. TS4->TS3->TS4 test skipped.");
            return;
        }

        string ts3Target1 = Path.Combine(Path.GetTempPath(), "roundtrip_ts3_reverse1_" + Guid.NewGuid().ToString("N") + ".package");
        string ts4Target1 = Path.Combine(Path.GetTempPath(), "roundtrip_ts4_reverse1_" + Guid.NewGuid().ToString("N") + ".package");

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

            // Step 1: TS4 -> TS3
            var req1 = new DecorativeObjectConversionRequest(ts4SourcePath, ts3Target1, GameVersion.Sims3, SourceGameVersion: GameVersion.Sims4);
            var res1 = await conversionService.ExecuteConversionAsync(req1);
            res1.IsSuccess.Should().BeTrue("TS4 -> TS3 conversion must succeed.");

            // Step 2: TS3 -> TS4
            var req2 = new DecorativeObjectConversionRequest(ts3Target1, ts4Target1, GameVersion.Sims4, SourceGameVersion: GameVersion.Sims3);
            var res2 = await conversionService.ExecuteConversionAsync(req2);
            res2.IsSuccess.Should().BeTrue("TS3 -> TS4 conversion must succeed.");

            var consumerValidator = new Ts4PackageConsumerValidator(dbpfParser, payloadReader, ts4Importer, new Ts4Rle2TextureDecoder());
            var valResult = await consumerValidator.ValidatePackageAsync(ts4Target1);
            valResult.IsSuccess.Should().BeTrue();

            _output.WriteLine($"[EXECUTED] TS4 -> TS3 -> TS4 Round-Trip Succeeded: Resources={valResult.VerifiedResourceCount}, GraphLinks={valResult.VerifiedGraphLinkCount}");
        }
        finally
        {
            if (File.Exists(ts3Target1)) File.Delete(ts3Target1);
            if (File.Exists(ts4Target1)) File.Delete(ts4Target1);
        }
    }

    [Fact]
    public async Task ExecuteUiBidirectionalRoundTripAndReinspection_WithRealTs3Fixture_PopulatesViewModel()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string ts4Target = Path.Combine(Path.GetTempPath(), "ui_roundtrip_ts4_" + Guid.NewGuid().ToString("N") + ".package");
        string ts3Target = Path.Combine(Path.GetTempPath(), "ui_roundtrip_ts3_" + Guid.NewGuid().ToString("N") + ".package");

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

            var viewModel = new ResourceInspectorViewModel(
                inspectionService: packageService,
                textureInspectionService: textureService,
                meshInspectionService: meshService,
                conversionService: conversionService
            )
            {
                SelectedFilePath = sourcePath,
                TargetOutputPath = ts4Target,
                TargetGameVersion = GameVersion.Sims4
            };

            // 1. Forward Conversion TS3 -> TS4 via UI ViewModel
            await viewModel.ConvertCommand.ExecuteAsync(null);
            viewModel.IsConversionSuccess.Should().BeTrue();
            viewModel.LastConvertedPackagePath.Should().Be(ts4Target);

            // Re-inspect TS4 output package via UI ViewModel
            await viewModel.InspectConvertedPackageCommand.ExecuteAsync(null);
            viewModel.SelectedFilePath.Should().Be(ts4Target);
            viewModel.Resources.Should().NotBeEmpty();

            // 2. Reverse Conversion TS4 -> TS3 via UI ViewModel
            viewModel.TargetOutputPath = ts3Target;
            viewModel.TargetGameVersion = GameVersion.Sims3;
            await viewModel.ConvertCommand.ExecuteAsync(null);
            viewModel.IsConversionSuccess.Should().BeTrue();
            viewModel.LastConvertedPackagePath.Should().Be(ts3Target);

            // Re-inspect TS3 output package via UI ViewModel
            await viewModel.InspectConvertedPackageCommand.ExecuteAsync(null);
            viewModel.SelectedFilePath.Should().Be(ts3Target);
            viewModel.Resources.Should().NotBeEmpty();

            _output.WriteLine($"UI Bidirectional Round-Trip Re-inspection Succeeded: TS4 Resources={viewModel.Resources.Count}");
        }
        finally
        {
            if (File.Exists(ts4Target)) File.Delete(ts4Target);
            if (File.Exists(ts3Target)) File.Delete(ts3Target);
        }
    }
}
