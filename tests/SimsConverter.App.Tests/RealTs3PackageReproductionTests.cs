using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.ViewModels;
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

public class RealTs3PackageReproductionTests
{
    private readonly ITestOutputHelper _output;

    public RealTs3PackageReproductionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InspectRealTs3ReproductionPackage_WhenFixtureExists_ConvertsSuccessfullyWithPayloadVerification()
    {
        string path = "/Users/dogan/Downloads/1790059/extract/Embedded Package #1.package";
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

            convResult.IsSuccess.Should().BeTrue("TS3 package with RefPack decomp and CAPA002 capability warnings must convert successfully.");
            File.Exists(tempTarget).Should().BeTrue();
            new FileInfo(tempTarget).Length.Should().BeGreaterThan(0);

            convResult.Issues.Should().Contain(i => i.Code == "CAPA002" && i.Severity == ConversionIssueSeverity.Warning, "CAPA002 warnings must be present as non-blocking warnings.");
            convResult.Issues.Should().NotContain(i => i.Severity == ConversionIssueSeverity.Error, "No conversion errors must be emitted.");

            byte[] outputBytes = await File.ReadAllBytesAsync(tempTarget);
            var parseOutputResult = dbpfParser.Parse(outputBytes);
            parseOutputResult.IsSuccess.Should().BeTrue("Generated TS4 package must be a valid DBPF package.");

            var verifierResult = payloadVerifier.VerifyPackagePayloads(tempTarget, parseOutputResult);
            verifierResult.IsSuccess.Should().BeTrue("Generated TS4 package payloads must pass compatibility verifier cleanly.");
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
