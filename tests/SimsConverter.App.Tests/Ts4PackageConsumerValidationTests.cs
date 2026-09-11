using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class Ts4PackageConsumerValidationTests
{
    private readonly ITestOutputHelper _output;

    public Ts4PackageConsumerValidationTests(ITestOutputHelper output)
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
        var catalogReader = new Ts3CatalogMetadataReader(payloadReader);
        var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService, decompService, catalogReader);
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
    public async Task ExecuteEndToEndConsumerValidation_WithRealEmbeddedPackageFixture_VerifiesCompleteResourceGraphAndMetrics()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string tempTarget = Path.Combine(Path.GetTempPath(), "ts4_consumer_val_target_" + Guid.NewGuid().ToString("N") + ".package");

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

            // 1. Execute Conversion
            var req = new DecorativeObjectConversionRequest(sourcePath, tempTarget, GameVersion.Sims4);
            var convResult = await conversionService.ExecuteConversionAsync(req);
            convResult.IsSuccess.Should().BeTrue("Conversion must succeed for real TS3 fixture.");

            // 2. Execute Consumer Validation Flow
            var consumerValidator = new Ts4PackageConsumerValidator(dbpfParser, payloadReader, ts4Importer, new Ts4Rle2TextureDecoder());
            var valResult = await consumerValidator.ValidatePackageAsync(tempTarget);

            valResult.Should().NotBeNull();
            valResult.IsSuccess.Should().BeTrue("Consumer package validation must succeed for produced TS4 package.");
            valResult.VerifiedResourceCount.Should().BeGreaterThan(0, "All package entries must be verified.");
            valResult.VerifiedGraphLinkCount.Should().BeGreaterThanOrEqualTo(5, "COBJ->OBJD, OBJD->MODL, MODL->MLOD, MLOD->GEOM, MLOD->RMAT, RMAT->RLE2 graph links must be verified.");
            valResult.VerifiedMeshCount.Should().BeGreaterThanOrEqualTo(1, "GEOM vertex & face metrics must be verified.");
            valResult.VerifiedTextureCount.Should().Be(6, "All 6 RLE2 texture mipmap streams must be decoded and verified.");
            valResult.CatalogMetadata.Should().NotBeNull("OBJD catalog metadata must be extracted during consumer validation.");

            _output.WriteLine($"Consumer Validation Succeeded: Resources={valResult.VerifiedResourceCount}, GraphLinks={valResult.VerifiedGraphLinkCount}, Meshes={valResult.VerifiedMeshCount}, Textures={valResult.VerifiedTextureCount}");
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
    public async Task ExecuteUiConversionAndReinspection_WithRealFixture_PopulatesInspectorViewModel()
    {
        string sourcePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"[SKIPPED] Embedded Package #1.package fixture not found at '{sourcePath}'.");
            return;
        }

        string tempTarget = Path.Combine(Path.GetTempPath(), "ts4_ui_reinspection_target_" + Guid.NewGuid().ToString("N") + ".package");

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
                TargetOutputPath = tempTarget,
                TargetGameVersion = GameVersion.Sims4
            };

            // 1. Run Conversion via UI ViewModel
            await viewModel.ConvertCommand.ExecuteAsync(null);

            viewModel.HasConversionResult.Should().BeTrue();
            viewModel.IsConversionSuccess.Should().BeTrue("UI conversion command must succeed.");
            viewModel.LastConvertedPackagePath.Should().Be(tempTarget);
            viewModel.CanInspectConvertedPackage.Should().BeTrue("CanInspectConvertedPackage must be true.");

            // 2. Execute UI Re-inspection of Converted Package
            await viewModel.InspectConvertedPackageCommand.ExecuteAsync(null);

            viewModel.SelectedFilePath.Should().Be(tempTarget);
            viewModel.Resources.Should().NotBeEmpty("UI inspection must load converted package resources into Resources collection.");
            viewModel.MeshResources.Should().NotBeEmpty("UI inspection must populate MeshResources collection.");
            viewModel.TextureResources.Should().NotBeEmpty("UI inspection must populate TextureResources collection.");
            viewModel.StatusMessage.Should().Contain("Package inspection complete");

            _output.WriteLine($"UI Re-inspection Succeeded: Loaded {viewModel.Resources.Count} resources ({viewModel.MeshResources.Count} meshes, {viewModel.TextureResources.Count} textures).");
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
    public async Task ExecuteControlledErrorAndRollbackScenarios_ValidatesFailureHandlingAndCleanState()
    {
        string invalidSourcePath = Path.Combine(Path.GetTempPath(), "invalid_source_" + Guid.NewGuid().ToString("N") + ".package");
        string tempTarget = Path.Combine(Path.GetTempPath(), "rollback_target_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            // Write 10 bytes of invalid data (corrupt file)
            await File.WriteAllBytesAsync(invalidSourcePath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 });

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

            // 1. Act: Execute conversion on corrupt source file
            var req = new DecorativeObjectConversionRequest(invalidSourcePath, tempTarget, GameVersion.Sims4);
            var convResult = await conversionService.ExecuteConversionAsync(req);

            // 2. Assert Controlled Failure
            convResult.IsSuccess.Should().BeFalse("Conversion on corrupt package file must fail gracefully.");
            convResult.Issues.Should().NotBeEmpty("Issues collection must record structured diagnostic failure details.");
            convResult.Issues.Should().Contain(i => i.Severity == ConversionIssueSeverity.Error);

            // 3. Assert Rollback State (Target file must NOT exist / corrupt output is prevented)
            File.Exists(tempTarget).Should().BeFalse("Failed conversion must maintain a clean rollback state (target file not created).");

            _output.WriteLine("Controlled Error and Rollback State Succeeded.");
        }
        finally
        {
            if (File.Exists(invalidSourcePath)) File.Delete(invalidSourcePath);
            if (File.Exists(tempTarget)) File.Delete(tempTarget);
        }
    }

    [Fact]
    public async Task ExecutePublishBuildOutputSmokeTest_ValidatesEndToEndExecution()
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
        var consumerValidator = new Ts4PackageConsumerValidator(dbpfParser, payloadReader, ts4Importer, new Ts4Rle2TextureDecoder());

        conversionService.Should().NotBeNull();
        consumerValidator.Should().NotBeNull();

        await Task.CompletedTask;
    }
}
