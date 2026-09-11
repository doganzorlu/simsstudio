using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.Application.Tests;

public class Ts3ObjectModelDecompositionRealFixtureTests
{
    private readonly ITestOutputHelper _output;

    public Ts3ObjectModelDecompositionRealFixtureTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DecomposeAsync_WhenOnyxExportedPackageFixturePresent_ValidatesDecompositionCounts()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string localFixturesDir = Path.Combine(solutionDir, "fixtures", "local");

        if (!Directory.Exists(localFixturesDir))
        {
            _output.WriteLine($"[SKIPPED] Local fixtures directory does not exist at '{localFixturesDir}'.");
            return;
        }

        string[] packageFiles = Directory.GetFiles(localFixturesDir, "*.package", SearchOption.AllDirectories);
        if (packageFiles.Length == 0)
        {
            _output.WriteLine($"[SKIPPED] No .package files found in '{localFixturesDir}'.");
            return;
        }

        var dbpfParser = new DbpfPackageParser();
        var pkgInspectionService = new PackageInspectionService(dbpfParser);
        var payloadReader = new PackageResourcePayloadReader();
        var metadataReader = new Ts3ObjectModelMetadataReader();
        var decompService = new Ts3ObjectModelDecompositionService(pkgInspectionService, payloadReader, metadataReader);

        bool evaluatedTargetFixture = false;

        foreach (var packagePath in packageFiles)
        {
            string fileName = Path.GetFileName(packagePath);

            var decompResult = await decompService.DecomposeAsync(packagePath);
            if (fileName.Contains("Embedded Package", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Onyx", StringComparison.OrdinalIgnoreCase))
            {
                evaluatedTargetFixture = true;

                // Acceptance criteria: 1 MODL, 2 MLOD, 1 RIG, 1 RSLT
                decompResult.ModlCount.Should().Be(1, "Onyx target package fixture contains exactly 1 MODL resource (0x01661233).");
                decompResult.MlodCount.Should().Be(2, "Onyx target package fixture contains exactly 2 MLOD resources (0x01D10F34).");
                decompResult.RigCount.Should().Be(1, "Onyx target package fixture contains exactly 1 RIG resource (0x8EAF13DE).");
                decompResult.RsltCount.Should().Be(1, "Onyx target package fixture contains exactly 1 RSLT resource (0xD3044521).");
                decompResult.ModelMetadataResults.Should().HaveCount(3, "MODL (1) and MLOD (2) payload metadata results should be extracted.");

                // RefPack (0xFB10) decompressor successfully decompresses compressed MODL/MLOD payloads.
                decompResult.IsSuccess.Should().BeTrue("RefPack compressed payloads must be decompressed successfully.");
                decompResult.HasDecompositionMetadata.Should().BeTrue("Decompressed RefPack payload metadata is available.");
            }
        }

        if (!evaluatedTargetFixture)
        {
            _output.WriteLine("[SKIPPED] No Onyx or Embedded Package target object fixture found to assert strict 1 MODL, 2 MLOD, 1 RIG, 1 RSLT counts.");
        }
    }

    [Fact]
    public async Task BuildGraphAsync_WhenOnyxExportedPackageFixturePresent_EvaluatesDecompositionGraphResolution()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string localFixturesDir = Path.Combine(solutionDir, "fixtures", "local");

        if (!Directory.Exists(localFixturesDir))
        {
            _output.WriteLine($"[SKIPPED] Local fixtures directory does not exist at '{localFixturesDir}'.");
            return;
        }

        string[] packageFiles = Directory.GetFiles(localFixturesDir, "*.package", SearchOption.AllDirectories);
        if (packageFiles.Length == 0)
        {
            _output.WriteLine($"[SKIPPED] No .package files found in '{localFixturesDir}'.");
            return;
        }

        var dbpfParser = new DbpfPackageParser();
        var pkgInspectionService = new PackageInspectionService(dbpfParser);
        var payloadReader = new PackageResourcePayloadReader();
        var metadataReader = new Ts3ObjectModelMetadataReader();
        var decompService = new Ts3ObjectModelDecompositionService(pkgInspectionService, payloadReader, metadataReader);

        var geomImporter = new Ts3GeomCanonicalMeshImporter();
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Mesh.Services.Ts4GeomMetadataReader(), new Domain.Services.CanonicalMeshValidator());
        var classifier = new MeshResourceClassifier();
        var meshInspectionService = new MeshInspectionService(pkgInspectionService, classifier, geomImporter, ts4Importer, payloadReader);

        var texClassifier = new Textures.Services.TextureResourceClassifier();
        var texInspectionService = new TextureInspectionService(pkgInspectionService, texClassifier);

        var graphBuilder = new DecorativeObjectSourceGraphBuilder(pkgInspectionService, meshInspectionService, texInspectionService, decompService);

        foreach (var packagePath in packageFiles)
        {
            string fileName = Path.GetFileName(packagePath);
            if (fileName.Contains("Embedded Package", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Onyx", StringComparison.OrdinalIgnoreCase))
            {
                var graph = await graphBuilder.BuildGraphAsync(packagePath);

                graph.ObjectModelDecomposition.Should().NotBeNull();
                graph.ObjectModelDecomposition!.TotalModelCount.Should().Be(3);
                graph.IsSourceGraphReady.Should().BeFalse();
                graph.Issues.Should().Contain(i => i.Code == "CONVG003");
            }
        }
    }

    [Fact]
    public async Task BuildBundleAsync_WhenOnyxExportedPackageFixturePresent_EvaluatesConversionInputBundle()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string localFixturesDir = Path.Combine(solutionDir, "fixtures", "local");

        if (!Directory.Exists(localFixturesDir))
        {
            _output.WriteLine($"[SKIPPED] Local fixtures directory does not exist at '{localFixturesDir}'.");
            return;
        }

        string[] packageFiles = Directory.GetFiles(localFixturesDir, "*.package", SearchOption.AllDirectories);
        if (packageFiles.Length == 0)
        {
            _output.WriteLine($"[SKIPPED] No .package files found in '{localFixturesDir}'.");
            return;
        }

        var dbpfParser = new DbpfPackageParser();
        var pkgInspectionService = new PackageInspectionService(dbpfParser);
        var payloadReader = new PackageResourcePayloadReader();
        var metadataReader = new Ts3ObjectModelMetadataReader();
        var decompService = new Ts3ObjectModelDecompositionService(pkgInspectionService, payloadReader, metadataReader);

        var validator = new Domain.Services.CanonicalMeshValidator();
        var geomImporter = new Ts3GeomCanonicalMeshImporter(new Mesh.Services.Ts3GeomMetadataReader(), validator);
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Mesh.Services.Ts4GeomMetadataReader(), validator);
        var classifier = new MeshResourceClassifier();
        var meshInspectionService = new MeshInspectionService(pkgInspectionService, classifier, geomImporter, ts4Importer, payloadReader);

        var texClassifier = new Textures.Services.TextureResourceClassifier();
        var texInspectionService = new TextureInspectionService(pkgInspectionService, texClassifier);

        var graphBuilder = new DecorativeObjectSourceGraphBuilder(pkgInspectionService, meshInspectionService, texInspectionService, decompService);
        var bundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, geomImporter, validator);

        foreach (var packagePath in packageFiles)
        {
            string fileName = Path.GetFileName(packagePath);
            if (fileName.Contains("Embedded Package", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Onyx", StringComparison.OrdinalIgnoreCase))
            {
                var graph = await graphBuilder.BuildGraphAsync(packagePath);
                var bundle = await bundleBuilder.BuildBundleAsync(graph, "onyx_output.package");

                bundle.Should().NotBeNull();
                bundle.SourcePackagePath.Should().Be(packagePath);
                bundle.TargetOutputPath.Should().Be("onyx_output.package");
                bundle.ObjectModelDecomposition.Should().NotBeNull();
                bundle.ResourceLinks.Should().BeEmpty("Zero speculative linking guaranteed.");

                foreach (var meshAsset in graph.MeshAssets)
                {
                    meshAsset.Entry.Should().NotBeNull("Real source mesh asset must carry non-null PackageResourceEntry metadata.");
                    meshAsset.Entry!.DataOffset.Should().BeGreaterThan(0, "Real package resource entries must preserve non-zero DataOffset.");
                }

                foreach (var meshBundle in bundle.MeshBundles)
                {
                    meshBundle.RawPayload.Should().NotBeNull("Mesh input bundle must store raw payload bytes extracted using real entry metadata.");
                    meshBundle.RawPayload!.Count.Should().BeGreaterThan(0, "Extracted mesh raw payload bytes must not be empty.");
                }
            }
        }
    }

    [Fact]
    public async Task BuildPackageAsync_WhenOnyxExportedPackageFixturePresent_WritesValidTs4PackageAndReopensWithDbpfParser()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string localFixturesDir = Path.Combine(solutionDir, "fixtures", "local");

        if (!Directory.Exists(localFixturesDir))
        {
            _output.WriteLine($"[SKIPPED] Local fixtures directory does not exist at '{localFixturesDir}'.");
            return;
        }

        string[] packageFiles = Directory.GetFiles(localFixturesDir, "*.package", SearchOption.AllDirectories);
        if (packageFiles.Length == 0)
        {
            _output.WriteLine($"[SKIPPED] No .package files found in '{localFixturesDir}'.");
            return;
        }

        var dbpfParser = new DbpfPackageParser();
        var pkgInspectionService = new PackageInspectionService(dbpfParser);
        var payloadReader = new PackageResourcePayloadReader();
        var metadataReader = new Ts3ObjectModelMetadataReader();
        var decompService = new Ts3ObjectModelDecompositionService(pkgInspectionService, payloadReader, metadataReader);

        var validator = new Domain.Services.CanonicalMeshValidator();
        var geomImporter = new Ts3GeomCanonicalMeshImporter(new Mesh.Services.Ts3GeomMetadataReader(), validator);
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Mesh.Services.Ts4GeomMetadataReader(), validator);
        var classifier = new MeshResourceClassifier();
        var meshInspectionService = new MeshInspectionService(pkgInspectionService, classifier, geomImporter, ts4Importer, payloadReader);

        var texClassifier = new Textures.Services.TextureResourceClassifier();
        var texInspectionService = new TextureInspectionService(pkgInspectionService, texClassifier);

        var graphBuilder = new DecorativeObjectSourceGraphBuilder(pkgInspectionService, meshInspectionService, texInspectionService, decompService);
        var bundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, geomImporter, validator);
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();
        var packageWriter = new DecorativeObjectPackageWriter();

        string tempOutputDir = Path.Combine(Path.GetTempPath(), "onyx_writer_fixture_test_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempOutputDir);

            foreach (var packagePath in packageFiles)
            {
                string fileName = Path.GetFileName(packagePath);
                if (fileName.Contains("Embedded Package", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("Onyx", StringComparison.OrdinalIgnoreCase))
                {
                    string outputPath = Path.Combine(tempOutputDir, "onyx_converted_ts4.package");
                    var graph = await graphBuilder.BuildGraphAsync(packagePath);
                    var bundle = await bundleBuilder.BuildBundleAsync(graph, outputPath);

                    if (bundle.IsBundleValid)
                    {
                        var plan = await planBuilder.BuildWritePlanAsync(bundle);
                        plan.IsPlanValid.Should().BeTrue();
                        plan.ResourceSetReport.Should().NotBeNull("Assembly report must be generated for write plan.");
                        plan.ResourceSetReport!.TotalResourceCount.Should().Be(plan.PlannedResources.Count);
                        plan.ResourceSetReport.MeshCount.Should().BeGreaterThan(0);
                        plan.ResourceSetReport.RigCount.Should().BeGreaterThan(0);
                        plan.ResourceSetReport.RsltCount.Should().BeGreaterThan(0);

                        var writeResult = await packageWriter.WritePackageAsync(plan);
                        writeResult.IsSuccess.Should().BeTrue();
                        File.Exists(outputPath).Should().BeTrue();

                        byte[] writtenBytes = await File.ReadAllBytesAsync(outputPath);
                        var parseResult = dbpfParser.Parse(writtenBytes);

                        parseResult.IsSuccess.Should().BeTrue("Written TS4 output package file must parse cleanly via DBPF parser.");
                        parseResult.Header!.MajorVersion.Should().Be(2, "TS4 output package major version must be 2.");
                        parseResult.Header.IndexEntryCount.Should().Be(plan.PlannedResources.Count);
                        parseResult.Entries.Should().HaveCount(plan.PlannedResources.Count);

                        parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x319E4F1D, "Parsed package must contain COBJ resource.");
                        parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x01661233, "Parsed package must contain MODL resource.");
                        parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x01D10F34, "Parsed package must contain MLOD resource.");
                        parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x2172D019, "Parsed package must contain Material resource.");

                        foreach (var entry in parseResult.Entries)
                        {
                            entry.DataOffset.Should().BeGreaterThanOrEqualTo(96, "Resource offset must be at or after 96-byte DBPF header.");
                            ((long)entry.DataOffset + entry.CompressedSize).Should().BeLessThanOrEqualTo(writtenBytes.Length, "Resource offset + compressed size must fit inside container file boundary.");
                        }
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempOutputDir))
            {
                Directory.Delete(tempOutputDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteConversionAsync_WhenOnyxExportedPackageFixturePresent_PerformsFirstEndToEndConversionAndValidatesPayloads()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string localFixturesDir = Path.Combine(solutionDir, "fixtures", "local");

        if (!Directory.Exists(localFixturesDir))
        {
            _output.WriteLine($"[SKIPPED] Local fixtures directory does not exist at '{localFixturesDir}'.");
            return;
        }

        string[] packageFiles = Directory.GetFiles(localFixturesDir, "*.package", SearchOption.AllDirectories);
        if (packageFiles.Length == 0)
        {
            _output.WriteLine($"[SKIPPED] No .package files found in '{localFixturesDir}'.");
            return;
        }

        var dbpfParser = new DbpfPackageParser();
        var pkgInspectionService = new PackageInspectionService(dbpfParser);
        var payloadReader = new PackageResourcePayloadReader();
        var metadataReader = new Ts3ObjectModelMetadataReader();
        var decompService = new Ts3ObjectModelDecompositionService(pkgInspectionService, payloadReader, metadataReader);

        var validator = new Domain.Services.CanonicalMeshValidator();
        var geomImporter = new Ts3GeomCanonicalMeshImporter(new Mesh.Services.Ts3GeomMetadataReader(), validator);
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Mesh.Services.Ts4GeomMetadataReader(), validator);
        var classifier = new MeshResourceClassifier();
        var meshInspectionService = new MeshInspectionService(pkgInspectionService, classifier, geomImporter, ts4Importer, payloadReader);

        var texClassifier = new Textures.Services.TextureResourceClassifier();
        var texInspectionService = new TextureInspectionService(pkgInspectionService, texClassifier);

        var graphBuilder = new DecorativeObjectSourceGraphBuilder(pkgInspectionService, meshInspectionService, texInspectionService, decompService);
        var bundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, geomImporter, validator);
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();
        var packageWriter = new DecorativeObjectPackageWriter();
        var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

        var conversionService = new DecorativeObjectConversionService(
            pkgInspectionService,
            meshInspectionService,
            texInspectionService,
            graphBuilder,
            bundleBuilder,
            planBuilder,
            packageWriter,
            payloadVerifier,
            dbpfParser
        );

        string tempOutputDir = Path.Combine(Path.GetTempPath(), "onyx_e2e_fixture_test_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempOutputDir);

            foreach (var packagePath in packageFiles)
            {
                string fileName = Path.GetFileName(packagePath);
                if (fileName.Contains("Embedded Package", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("Onyx", StringComparison.OrdinalIgnoreCase))
                {
                    string outputPath = Path.Combine(tempOutputDir, "onyx_e2e_converted_ts4.package");
                    var request = new DecorativeObjectConversionRequest(packagePath, outputPath, Domain.Enums.GameVersion.Sims4);

                    var result = await conversionService.ExecuteConversionAsync(request);

                    if (result.Plan != null && result.Plan.IsFeasible && result.Plan.InputBundle != null && result.Plan.InputBundle.IsBundleValid)
                    {
                        result.IsSuccess.Should().BeTrue("End-to-end conversion of Onyx fixture must succeed.");
                        File.Exists(outputPath).Should().BeTrue("Output TS4 package file must exist.");

                        result.Plan.Steps.Should().Contain(s => s.StepId == "STEP-08-EXEC-CONV" && s.Status == Domain.Enums.DecorativeObjectConversionStepStatus.Completed);

                        // Verify re-opening written package
                        byte[] writtenBytes = await File.ReadAllBytesAsync(outputPath);
                        var parseResult = dbpfParser.Parse(writtenBytes);
                        parseResult.IsSuccess.Should().BeTrue("Written TS4 package must parse cleanly via DBPF parser.");

                        // Verify payload compatibility
                        var compatResult = payloadVerifier.VerifyPackagePayloads(outputPath, parseResult);
                        compatResult.IsSuccess.Should().BeTrue("All payloads and TGI graph links in written package must pass verification.");
                        compatResult.TotalResourcesVerified.Should().BeGreaterThan(0);
                        compatResult.VerifiedTgiLinkCount.Should().BeGreaterThan(0);
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempOutputDir))
            {
                Directory.Delete(tempOutputDir, recursive: true);
            }
        }
    }
}
