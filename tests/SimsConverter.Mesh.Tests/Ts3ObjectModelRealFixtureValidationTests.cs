using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Package.Services;
using SimsConverter.Mesh.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.Mesh.Tests;

public class Ts3ObjectModelRealFixtureValidationTests
{
    private readonly ITestOutputHelper _output;

    public Ts3ObjectModelRealFixtureValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ValidateRealPackageObjectModelResources_WhenPresent_InspectsMODLAndMLODEntries()
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
        var payloadReader = new PackageResourcePayloadReader();
        var modelReader = new Ts3ObjectModelMetadataReader();

        foreach (var packagePath in packageFiles)
        {
            string fileName = Path.GetFileName(packagePath);
            var dbpfResult = await dbpfParser.ParseFileAsync(packagePath);

            if (!dbpfResult.IsSuccess)
            {
                _output.WriteLine($"[WARN] Could not parse package '{fileName}': {dbpfResult.Issues.FirstOrDefault()?.Message}");
                continue;
            }

            var modlEntries = dbpfResult.Entries.Where(e => e.Id.TypeId == 0x01661233u).ToList();
            var mlodEntries = dbpfResult.Entries.Where(e => e.Id.TypeId == 0x01D10F34u).ToList();

            if (modlEntries.Count == 0 && mlodEntries.Count == 0)
            {
                _output.WriteLine($"[INFO] Package '{fileName}' contains zero MODL/MLOD resources.");
                continue;
            }

            _output.WriteLine($"[SUCCESS] Package '{fileName}' contains {modlEntries.Count} MODL resource(s) and {mlodEntries.Count} MLOD resource(s).");

            foreach (var modl in modlEntries)
            {
                var payloadResult = await payloadReader.ReadPayloadAsync(packagePath, modl);
                if (payloadResult.IsSuccess && payloadResult.Payload != null)
                {
                    var metaResult = modelReader.Read(payloadResult.Payload, modl.Id);
                    metaResult.IsSuccess.Should().BeTrue($"Metadata reading for decompressed MODL entry {modl.Id.FormattedKey} should succeed");
                    metaResult.ModelKind.Should().Be(Models.Ts3ObjectModelKind.Modl);
                }
                else
                {
                    _output.WriteLine($"[INFO] MODL entry '{modl.Id.FormattedKey}' read status: {payloadResult.Issues.FirstOrDefault()?.Code}");
                }
            }

            foreach (var mlod in mlodEntries)
            {
                var payloadResult = await payloadReader.ReadPayloadAsync(packagePath, mlod);
                if (payloadResult.IsSuccess && payloadResult.Payload != null)
                {
                    var metaResult = modelReader.Read(payloadResult.Payload, mlod.Id);
                    metaResult.IsSuccess.Should().BeTrue($"Metadata reading for decompressed MLOD entry {mlod.Id.FormattedKey} should succeed");
                    metaResult.ModelKind.Should().Be(Models.Ts3ObjectModelKind.Mlod);
                }
                else
                {
                    _output.WriteLine($"[INFO] MLOD entry '{mlod.Id.FormattedKey}' read status: {payloadResult.Issues.FirstOrDefault()?.Code}");
                }
            }

            if (fileName.Contains("Embedded Package", StringComparison.OrdinalIgnoreCase) || fileName.Contains("Onyx", StringComparison.OrdinalIgnoreCase))
            {
                modlEntries.Count.Should().Be(1, "Target object package fixture contains exactly 1 MODL resource index entry.");
                mlodEntries.Count.Should().Be(2, "Target object package fixture contains exactly 2 MLOD resource index entries.");
            }
        }
    }
}
