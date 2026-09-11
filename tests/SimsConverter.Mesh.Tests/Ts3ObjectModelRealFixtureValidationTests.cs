using System;
using System.IO;
using System.Linq;
using System.Text;
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

            _output.WriteLine($"[PACKAGE INSPECTION] {fileName} total entries: {dbpfResult.Entries.Count}");
            foreach (var e in dbpfResult.Entries)
            {
                _output.WriteLine($"  - Entry: TypeId=0x{e.Id.TypeId:X8}, GroupId=0x{e.Id.GroupId:X8}, InstanceId=0x{e.Id.InstanceId:X16}, Size={e.DecompressedSize}");
            }

            _output.WriteLine($"[SUCCESS] Package '{fileName}' contains {modlEntries.Count} MODL resource(s) and {mlodEntries.Count} MLOD resource(s).");

            foreach (var modl in modlEntries)
            {
                var payloadResult = await payloadReader.ReadPayloadAsync(packagePath, modl);
                if (payloadResult.IsSuccess && payloadResult.Payload != null)
                {
                    byte[] p = payloadResult.Payload.ToArray();
                    _output.WriteLine($"[MODL PAYLOAD {modl.Id.FormattedKey}] Length={p.Length}");
                }
            }

            foreach (var mlod in mlodEntries)
            {
                var payloadResult = await payloadReader.ReadPayloadAsync(packagePath, mlod);
                if (payloadResult.IsSuccess && payloadResult.Payload != null)
                {
                    byte[] p = payloadResult.Payload.ToArray();
                    _output.WriteLine($"[MLOD PAYLOAD {mlod.Id.FormattedKey}] Length={p.Length}");

                    _output.WriteLine($"  Header (first 64 bytes):");
                    for (int h = 0; h < Math.Min(p.Length, 64); h += 4)
                    {
                        uint val = BitConverter.ToUInt32(p, h);
                        string charStr = "";
                        foreach (byte b in p.AsSpan(h, 4)) charStr += (b >= 32 && b <= 126) ? (char)b : '.';
                        _output.WriteLine($"    Offset 0x{h:X4} ({h}): 0x{val:X8} ({val}) [{charStr}]");
                    }

                    int vfrtPos = -1, vbufPos = -1, ibufPos = -1;
                    for (int i = 0; i <= p.Length - 4; i++)
                    {
                        uint magic = BitConverter.ToUInt32(p, i);
                        if (magic == 0x54524656) { vfrtPos = i; _output.WriteLine($"  VFRT found at 0x{i:X4} ({i})"); }
                        else if (magic == 0x46554256) { vbufPos = i; _output.WriteLine($"  VBUF found at 0x{i:X4} ({i})"); }
                        else if (magic == 0x46554249) { ibufPos = i; _output.WriteLine($"  IBUF found at 0x{i:X4} ({i})"); }
                    }

                    foreach (int pos in new[] { 0x06A8, 0x9788 })
                    {
                        if (pos >= 0 && pos + 128 <= p.Length)
                        {
                            _output.WriteLine($"  At 0x{pos:X4} ({pos}):");
                            for (int k = 0; k < 128; k += 16)
                            {
                                string hex = BitConverter.ToString(p, pos + k, 16);
                                string vals = "";
                                for (int v = 0; v < 16; v += 4)
                                    vals += $" 0x{BitConverter.ToUInt32(p, pos + k + v):X8}";
                                _output.WriteLine($"    +0x{k:X2}: {hex} |{vals}");
                            }
                        }
                    }
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
