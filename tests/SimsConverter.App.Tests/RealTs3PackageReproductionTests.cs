using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class RealTs3PackageReproductionTests
{
    private readonly ITestOutputHelper _output;

    public RealTs3PackageReproductionTests(ITestOutputHelper output)
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

    [Fact]
    public async Task InspectRealTs3ReproductionPackage_WhenFixtureExists_ConvertsSuccessfullyWithPayloadVerification()
    {
        string path = GetEmbeddedPackageFixturePath();
        if (!File.Exists(path))
        {
            _output.WriteLine($"[SKIPPED] Local reproduction TS3 package fixture not found at '{path}'. Test reported as SKIPPED.");
            return;
        }

        string tempTarget = Path.Combine(Path.GetTempPath(), "real_ts4_repro_output_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var metadataReader = new Ts3ObjectModelMetadataReader();
            var decompService = new Ts3ObjectModelDecompositionService(packageService, payloadReader, metadataReader);

            var validator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);

            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);

            var capabilityService = new DecorativeObjectConversionCapabilityService();
            var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService, decompService);
            var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, validator);
            var ts4ResourceGenerator = new DecorativeObjectTs4ResourceGenerator();
            var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader, ts4ResourceGenerator);
            var dbpfWriter = new DbpfPackageWriter();
            var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            var conversionService = new DecorativeObjectConversionService(
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

            var req = new DecorativeObjectConversionRequest(path, tempTarget, GameVersion.Sims4);
            var convResult = await conversionService.ExecuteConversionAsync(req);

            if (!convResult.IsSuccess)
            {
                foreach (var issue in convResult.Issues)
                {
                    _output.WriteLine($"Conversion Issue: [{issue.Severity}] {issue.Code} - {issue.Message}");
                }
            }

            convResult.IsSuccess.Should().BeTrue("Embedded Package #1.package contains MLOD geometry streams and must convert successfully.");

            var targetDbpf = await dbpfParser.ParseFileAsync(tempTarget);
            targetDbpf.IsSuccess.Should().BeTrue();
            targetDbpf.Entries.Should().Contain(e => e.Id.TypeId == Ts4ResourceTypeIds.Geom, "Extracted TS4 package must contain real GEOM entries (0x015A1849).");


        }
        finally
        {
            if (File.Exists(tempTarget))
            {
                File.Delete(tempTarget);
            }
        }
    }
}
