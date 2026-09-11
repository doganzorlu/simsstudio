using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.Mesh.Tests;

public class Ts3MlodGeometryDecoderTests
{
    private readonly ITestOutputHelper _output;

    public Ts3MlodGeometryDecoderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetEmbeddedPackageFixturePath()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string fixturePath = Path.Combine(solutionDir, "fixtures", "local", "Embedded Package #1.package");
        if (File.Exists(fixturePath))
        {
            return fixturePath;
        }
        return "/Users/dogan/Downloads/1790059/extract/Embedded Package #1.package";
    }

    [Fact]
    public async Task Decode_WithRealMlodPayload_EnforcesSuccessfulDecodingAndExactMetrics()
    {
        string packagePath = GetEmbeddedPackageFixturePath();
        if (!File.Exists(packagePath))
        {
            _output.WriteLine($"[SKIPPED] Fixture file not found: {packagePath}");
            return;
        }

        var dbpfParser = new DbpfPackageParser();
        var payloadReader = new PackageResourcePayloadReader();
        var decoder = new Ts3MlodGeometryDecoder();

        var dbpfResult = await dbpfParser.ParseFileAsync(packagePath);
        dbpfResult.IsSuccess.Should().BeTrue();

        var modlEntries = dbpfResult.Entries.Where(e => e.Id.TypeId == 0x01661233u).ToList();
        var mlodEntries = dbpfResult.Entries.Where(e => e.Id.TypeId == 0x01D10F34u).ToList();

        modlEntries.Should().HaveCount(1, "Fixture contains 1 MODL entry.");
        mlodEntries.Should().HaveCount(2, "Fixture contains 2 MLOD entries.");

        // 1. Test MODL entry containing VFRT/VBUF/IBUF
        var modlPayloadRes = await payloadReader.ReadPayloadAsync(packagePath, modlEntries[0]);
        modlPayloadRes.IsSuccess.Should().BeTrue();
        var modlDecodeRes = decoder.Decode(modlPayloadRes.Payload!, modlEntries[0].Id.FormattedKey);
        _output.WriteLine($"MODL Decode IsSuccess = {modlDecodeRes.IsSuccess}");
        foreach (var issue in modlDecodeRes.Issues)
        {
            _output.WriteLine($"  - [{issue.Severity}] {issue.Code}: {issue.Message}");
        }
        modlDecodeRes.IsSuccess.Should().BeTrue("MODL payload containing VFRT/VBUF/IBUF stream must decode successfully.");
        var modlMesh = modlDecodeRes.Mesh;
        modlMesh.Should().NotBeNull();
        modlMesh!.Vertices.Count.Should().Be(425, "MODL LOD0 stream vertex count must be exactly 425.");
        modlMesh.Faces.Count.Should().Be(249, "MODL LOD0 stream valid face count must be exactly 249.");

        // 2. Test MLOD entries containing VFRT/VBUF/IBUF
        int decodedCount = 0;
        foreach (var entry in mlodEntries)
        {
            var payloadResult = await payloadReader.ReadPayloadAsync(packagePath, entry);
            payloadResult.IsSuccess.Should().BeTrue();

            var decodeResult = decoder.Decode(payloadResult.Payload!, entry.Id.FormattedKey);
            if (decodeResult.IsSuccess)
            {
                decodedCount++;
                var mesh = decodeResult.Mesh;
                mesh.Should().NotBeNull();
                mesh!.Vertices.Count.Should().BeGreaterThan(0, "Extracted MLOD vertex count must be > 0.");
                mesh.Faces.Count.Should().BeGreaterThan(0, "Extracted MLOD face count must be > 0.");

                // Validate attribute fidelity for decoded vertices
                var firstVert = mesh.Vertices[0];
                firstVert.Position.Should().NotBeNull();
                firstVert.Normal.Should().NotBeNull();
                firstVert.Uv0.Should().NotBeNull();
                firstVert.BoneWeights.Should().NotBeNull().And.NotBeEmpty();

                _output.WriteLine($"Decoded MLOD {entry.Id.FormattedKey}: Vertices={mesh.Vertices.Count}, Faces={mesh.Faces.Count}");
            }
        }

        decodedCount.Should().BeGreaterThan(0, "At least 1 MLOD entry must decode successfully with valid geometry.");
    }

    [Fact]
    public void Decode_WhenBufferEmpty_ReturnsControlledFailure()
    {
        var decoder = new Ts3MlodGeometryDecoder();
        var result = decoder.Decode(Array.Empty<byte>());
        result.IsSuccess.Should().BeFalse();
        result.Mesh.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "MLOD001");
    }

    [Fact]
    public void Decode_WhenVfrtMarkerMissing_ReturnsControlledFailure()
    {
        var decoder = new Ts3MlodGeometryDecoder();

        // Construct fake payload with VBUF and IBUF, but missing VFRT
        byte[] payload = new byte[100];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0x46554256u); // VBUF
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(50, 4), 0x46554249u); // IBUF

        var result = decoder.Decode(payload, "TestMissingVfrt");
        result.IsSuccess.Should().BeFalse("Decoding must fail when VFRT chunk marker is missing.");
        result.Issues.Should().Contain(i => i.Code == "CONVG007");
    }

    [Fact]
    public void Decode_WhenVbufOrIbufMarkerMissing_ReturnsControlledFailure()
    {
        var decoder = new Ts3MlodGeometryDecoder();

        // Payload with VFRT only
        byte[] payload = new byte[100];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0x54524656u); // VFRT

        var result = decoder.Decode(payload, "TestMissingVbufIbuf");
        result.IsSuccess.Should().BeFalse("Decoding must fail when VBUF or IBUF chunk marker is missing.");
        result.Issues.Should().Contain(i => i.Code == "CONVG007");
    }

    [Fact]
    public void Decode_WhenInvalidStride_ReturnsControlledFailure()
    {
        var decoder = new Ts3MlodGeometryDecoder();

        byte[] payload = new byte[100];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0x54524656u); // VFRT
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4, 4), 1u); // version
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), 0u); // invalid stride = 0
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), 1u); // elemCount
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20, 4), 0x46554256u); // VBUF
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(50, 4), 0x46554249u); // IBUF

        var result = decoder.Decode(payload, "TestInvalidStride");
        result.IsSuccess.Should().BeFalse("Decoding must fail when VFRT stride is invalid (0).");
        result.Issues.Should().Contain(i => i.Code == "MLOD003");
    }

    [Fact]
    public void Decode_WhenTruncatedVbuf_ReturnsControlledFailure()
    {
        var decoder = new Ts3MlodGeometryDecoder();

        byte[] payload = new byte[60];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0x54524656u); // VFRT
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4, 4), 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), 28u); // stride = 28
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), 1u); // elemCount = 1

        // Position descriptor (usage = 1)
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16, 2), 0); // stream
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(18, 2), 0); // offset
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(20, 2), 7); // format = Float3
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(22, 2), 1); // usage = Position

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(24, 4), 0x46554256u); // VBUF
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(45, 4), 0x46554249u); // IBUF at +5 bytes from VBUF data (smaller than stride 28)

        var result = decoder.Decode(payload, "TestTruncatedVbuf");
        result.IsSuccess.Should().BeFalse("Decoding must fail when VBUF section length is smaller than stride.");
        result.Issues.Should().Contain(i => i.Code == "MLOD002");
    }

    [Fact]
    public void Decode_WhenTruncatedIbuf_ReturnsControlledFailure()
    {
        var decoder = new Ts3MlodGeometryDecoder();

        byte[] payload = new byte[70];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0x54524656u); // VFRT
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4, 4), 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), 28u); // stride = 28
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12, 4), 1u);
        // Elem descriptor (Position at offset 0)
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16, 2), 0); // stream
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(18, 2), 0); // offset = 0
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(20, 2), 7); // format = Float3
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(22, 2), 1); // usage = Position

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(24, 4), 0x46554256u); // VBUF
        // VBUF data 28 bytes (24..52)
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(56, 4), 0x46554249u); // IBUF
        // IBUF truncated with only 2 bytes remaining (56+16 = 72 > 70)

        var result = decoder.Decode(payload, "TestTruncatedIbuf");
        result.IsSuccess.Should().BeFalse("Decoding must fail when IBUF section is truncated.");
        result.Issues.Should().Contain(i => i.Code == "MLOD002");
    }
}
