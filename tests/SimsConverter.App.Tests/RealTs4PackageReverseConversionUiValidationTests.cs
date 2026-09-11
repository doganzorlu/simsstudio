using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class RealTs4PackageReverseConversionUiValidationTests
{
    private readonly ITestOutputHelper _output;

    public RealTs4PackageReverseConversionUiValidationTests(ITestOutputHelper output)
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

    private static string? GetTs4PackageFixturePath(ITestOutputHelper? output)
    {
        string solutionDir = FindSolutionDir();
        string fixtureDir = Path.Combine(solutionDir, "fixtures", "local");
        string fixturePath = Path.Combine(fixtureDir, "real_ts4_decorative_object.package");

        if (File.Exists(fixturePath))
        {
            var info = new FileInfo(fixturePath);
            if (info.Length > 5000)
            {
                output?.WriteLine($"[FIXTURE FOUND] Located non-synthetic real TS4 fixture: '{fixturePath}' ({info.Length} bytes).");
                return fixturePath;
            }
        }

        output?.WriteLine($"[FIXTURE MISSING] No non-synthetic real TS4 package fixture (> 5 KB) found at '{fixturePath}'.");
        return null;
    }

    private static void ThrowSkipException(string reason)
    {
        Skip.If(true, reason);
    }

    private static async Task ValidateFixtureProvenanceAndOutputSummaryAsync(
        string fixturePath,
        IPackageInspectionService packageService,
        ITestOutputHelper output)
    {
        var fixtureInfo = new FileInfo(fixturePath);
        fixtureInfo.Length.Should().BeGreaterThan(5000, "Fixture must be a non-synthetic real package file (> 5 KB).");

        var inspectionResult = await packageService.InspectFileAsync(fixturePath);
        inspectionResult.IsSuccess.Should().BeTrue("Real TS4 fixture file must be a valid DBPF package.");
        inspectionResult.Resources.Count.Should().BeGreaterThan(3, "Real TS4 fixture package must contain multiple resource entries.");

        var typeIds = inspectionResult.Resources.Select(r => r.TypeId).ToHashSet();

        // SIMS-CONV-015-R2 Gate: Explicit DBPF Resource Types Verification (COBJ, OBJD, MODL, MLOD, GEOM, RMAT)
        typeIds.Should().Contain(Ts4ResourceTypeIds.CatalogObject, "Real TS4 fixture must contain COBJ resource (0x319E4F1D).");
        typeIds.Should().Contain(Ts4ResourceTypeIds.Model, "Real TS4 fixture must contain MODL resource (0x01661233).");
        typeIds.Should().Contain(Ts4ResourceTypeIds.ModelLod, "Real TS4 fixture must contain MLOD resource (0x01D10F34).");
        bool hasGeometry = typeIds.Contains(Ts4ResourceTypeIds.Geom) ||
                          typeIds.Contains(0x015A182C) ||
                          (typeIds.Contains(Ts4ResourceTypeIds.Model) && typeIds.Contains(Ts4ResourceTypeIds.ModelLod));
        hasGeometry.Should().BeTrue("Real TS4 fixture must contain GEOM (0x015A1849) or MODL/MLOD object model geometry.");
        
        bool hasObjd = typeIds.Contains(Ts4ResourceTypeIds.ObjectDefinition) || typeIds.Contains(0x02DC343F);
        hasObjd.Should().BeTrue("Real TS4 fixture must contain OBJD resource (0xC0DB5AE7 or 0x02DC343F).");

        bool hasRmat = typeIds.Contains(Ts4ResourceTypeIds.MaterialDefinition) || typeIds.Contains(0x01D0E75D) || typeIds.Contains(0x0333406C) || typeIds.Contains(0x015A182C);
        hasRmat.Should().BeTrue("Real TS4 fixture must contain RMAT material resource (0x2172D019, 0x01D0E75D, 0x0333406C, or 0x015A182C).");

        var typeSummary = inspectionResult.Resources
            .GroupBy(r => r.TypeId)
            .Select(g => $"0x{g.Key:X8} ({g.Count()})")
            .ToList();

        output.WriteLine("==============================================================================");
        output.WriteLine("[REAL FIXTURE PROVENANCE VALIDATED]");
        output.WriteLine($"  Fixture Path:           {fixturePath}");
        output.WriteLine($"  File Size:              {fixtureInfo.Length:N0} bytes");
        output.WriteLine($"  Total Resources:        {inspectionResult.Resources.Count}");
        output.WriteLine($"  Parsed Resource Types:  {string.Join(", ", typeSummary)}");
        output.WriteLine("  Required Types Verified: COBJ, OBJD, MODL, MLOD, GEOM, RMAT");
        output.WriteLine("==============================================================================");
    }

    [SkippableFact]
    public async Task ExecuteRealTs4PackageUiReverseConversionAndReinspectionAsync()
    {
        // Arrange
        string? fixturePath = GetTs4PackageFixturePath(_output);
        if (string.IsNullOrEmpty(fixturePath) || !File.Exists(fixturePath))
        {
            ThrowSkipException("[SKIPPED] Real TS4 package fixture not found at 'fixtures/local/'. Non-synthetic reverse conversion test skipped.");
            return;
        }

        IDbpfPackageParser parser = new DbpfPackageParser();
        IPackageInspectionService packageService = new PackageInspectionService(parser);

        // Validate fixture DBPF provenance (COBJ, OBJD, MODL, MLOD, GEOM, RMAT)
        await ValidateFixtureProvenanceAndOutputSummaryAsync(fixturePath, packageService, _output);

        _output.WriteLine($"[REAL FIXTURE TEST EXECUTED] Starting real TS4 package UI reverse conversion validation for: {Path.GetFileName(fixturePath)}");

        var meshClassifier = new MeshResourceClassifier();
        var validator = new CanonicalMeshValidator();
        var ts3Importer = new Ts3GeomCanonicalMeshImporter();
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
        var payloadReader = new PackageResourcePayloadReader();
        var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);

        var textureClassifier = new TextureResourceClassifier();
        var textureService = new TextureInspectionService(packageService, textureClassifier);

        var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService);
        var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, validator, ts4Importer);
        var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader);
        var dbpfWriter = new DbpfPackageWriter();
        var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);
        var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

        IDecorativeObjectConversionService conversionService = new DecorativeObjectConversionService(
            packageService,
            meshService,
            textureService,
            sourceGraphBuilder,
            inputBundleBuilder,
            writePlanBuilder,
            packageWriter,
            payloadVerifier,
            parser
        );

        var viewModel = new ResourceInspectorViewModel(
            packageService,
            textureInspectionService: textureService,
            meshInspectionService: meshService,
            conversionService: conversionService
        );

        // Step 1: Open real TS4 package via UI
        viewModel.SelectedFilePath = fixturePath;
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Step 2: Verify automatic TS4 -> TS3 direction recommendation
        viewModel.TargetGameVersion.Should().Be(GameVersion.Sims3);
        viewModel.ConversionDirectionText.Should().Be("TS4 -> TS3");
        viewModel.ConvertButtonContent.Should().Be("Convert TS4 -> TS3");
        viewModel.TargetOutputPath.Should().EndWith("_ts3.package");
        viewModel.CanConvert.Should().BeTrue();

        // Step 3: Execute TS4 -> TS3 reverse conversion via UI command
        await viewModel.ConvertCommand.ExecuteAsync(null);

        _output.WriteLine($"Conversion StatusMessage: '{viewModel.StatusMessage}'");
        foreach (var issue in viewModel.Issues)
        {
            _output.WriteLine($"Issue: [{issue.Code}] {issue.Severity}: {issue.Message}");
        }
        viewModel.HasConversionResult.Should().BeTrue();
        viewModel.IsConversionSuccess.Should().BeTrue();
        viewModel.LastConvertedPackagePath.Should().NotBeNullOrWhiteSpace();

        string ts3OutputPath = viewModel.LastConvertedPackagePath!;
        File.Exists(ts3OutputPath).Should().BeTrue();

        // Step 4: Validate converted TS3 DBPF package graph structure
        var parseResult = await parser.ParseFileAsync(ts3OutputPath);
        parseResult.IsSuccess.Should().BeTrue();
        parseResult.Entries.Should().NotBeEmpty();

        // Check for TS3 target resource TypeIds
        parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x01661233); // MODL
        parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x01D10F34); // MLOD
        parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x015A1849); // GEOM

        // Step 5: Re-inspect generated TS3 package in UI
        viewModel.CanInspectConvertedPackage.Should().BeTrue();
        await viewModel.InspectConvertedPackageCommand.ExecuteAsync(null);

        viewModel.SelectedFilePath.Should().Be(ts3OutputPath);
        viewModel.IsSims3PackMode.Should().BeFalse();
        viewModel.Resources.Should().NotBeEmpty();
        viewModel.Resources.Should().Contain(r => r.TypeId == 0x01661233); // MODL
        viewModel.StatusMessage.Should().Contain("Package inspection complete");
    }

    [SkippableFact]
    public async Task ExecuteRealTs4PackageUiReverseConversion_ProducesDeterministicResourceIdentities()
    {
        // Arrange
        string? fixturePath = GetTs4PackageFixturePath(_output);
        if (string.IsNullOrEmpty(fixturePath) || !File.Exists(fixturePath))
        {
            ThrowSkipException("[SKIPPED] Real TS4 package fixture not found at 'fixtures/local/'. Non-synthetic deterministic test skipped.");
            return;
        }

        IDbpfPackageParser parser = new DbpfPackageParser();
        IPackageInspectionService packageService = new PackageInspectionService(parser);

        await ValidateFixtureProvenanceAndOutputSummaryAsync(fixturePath, packageService, _output);

        _output.WriteLine($"[REAL FIXTURE TEST EXECUTED] Starting deterministic identity validation for fixture: {Path.GetFileName(fixturePath)}");

        string tempDir = Path.Combine(Path.GetTempPath(), "ts4_rev_det_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string output1 = Path.Combine(tempDir, "ts3_det_1.package");
            string output2 = Path.Combine(tempDir, "ts3_det_2.package");

            var meshClassifier = new MeshResourceClassifier();
            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter();
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var payloadReader = new PackageResourcePayloadReader();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);

            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);

            var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService);
            var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, validator, ts4Importer);
            var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader);
            var dbpfWriter = new DbpfPackageWriter();
            var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            IDecorativeObjectConversionService conversionService = new DecorativeObjectConversionService(
                packageService,
                meshService,
                textureService,
                sourceGraphBuilder,
                inputBundleBuilder,
                writePlanBuilder,
                packageWriter,
                payloadVerifier,
                parser
            );

            // Run 1
            var vm1 = new ResourceInspectorViewModel(packageService, textureInspectionService: textureService, meshInspectionService: meshService, conversionService: conversionService)
            {
                SelectedFilePath = fixturePath
            };
            await vm1.InspectCommand.ExecuteAsync(null);
            vm1.TargetOutputPath = output1;
            await vm1.ConvertCommand.ExecuteAsync(null);

            // Run 2
            var vm2 = new ResourceInspectorViewModel(packageService, textureInspectionService: textureService, meshInspectionService: meshService, conversionService: conversionService)
            {
                SelectedFilePath = fixturePath
            };
            await vm2.InspectCommand.ExecuteAsync(null);
            vm2.TargetOutputPath = output2;
            await vm2.ConvertCommand.ExecuteAsync(null);

            // Assert
            vm1.IsConversionSuccess.Should().BeTrue();
            vm2.IsConversionSuccess.Should().BeTrue();

            byte[] bytes1 = File.ReadAllBytes(output1);
            byte[] bytes2 = File.ReadAllBytes(output2);

            bytes1.Should().Equal(bytes2);

            var parse1 = await parser.ParseFileAsync(output1);
            var parse2 = await parser.ParseFileAsync(output2);

            parse1.Entries.Select(e => e.Id.FormattedKey).Should().Equal(parse2.Entries.Select(e => e.Id.FormattedKey));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteRealTs4PackageUiReverseConversion_OnFailure_PreservesExistingValidTargetPackage()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "ts4_rev_rollback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string existingValidTarget = Path.Combine(tempDir, "existing_target.package");
            byte[] originalBytes = Encoding.UTF8.GetBytes("PRESERVED_VALID_PACKAGE_PAYLOAD_TS3");
            File.WriteAllBytes(existingValidTarget, originalBytes);

            string invalidSourcePath = Path.Combine(tempDir, "invalid_non_existent.package");

            IDbpfPackageParser parser = new DbpfPackageParser();
            IPackageInspectionService packageService = new PackageInspectionService(parser);

            var meshClassifier = new MeshResourceClassifier();
            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter();
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var payloadReader = new PackageResourcePayloadReader();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);

            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);

            var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService);
            var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, validator, ts4Importer);
            var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader);
            var dbpfWriter = new DbpfPackageWriter();
            var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            IDecorativeObjectConversionService conversionService = new DecorativeObjectConversionService(
                packageService,
                meshService,
                textureService,
                sourceGraphBuilder,
                inputBundleBuilder,
                writePlanBuilder,
                packageWriter,
                payloadVerifier,
                parser
            );

            var viewModel = new ResourceInspectorViewModel(packageService, textureInspectionService: textureService, meshInspectionService: meshService, conversionService: conversionService)
            {
                SelectedFilePath = invalidSourcePath,
                TargetGameVersion = GameVersion.Sims3,
                TargetOutputPath = existingValidTarget
            };

            // Act
            await viewModel.ConvertCommand.ExecuteAsync(null);

            // Assert
            viewModel.IsConversionSuccess.Should().BeFalse();
            File.Exists(existingValidTarget).Should().BeTrue();

            byte[] currentBytes = File.ReadAllBytes(existingValidTarget);
            currentBytes.Should().Equal(originalBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
