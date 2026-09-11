using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Models;
using SimsConverter.Mesh.Services;
using Xunit;

namespace SimsConverter.Mesh.Tests;

public class Ts3ObjectModelMetadataReaderTests
{
    [Fact]
    public void Read_NullResourceId_ReturnsFailureMODL000()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var result = reader.Read(new byte[] { 1, 2, 3, 4 }, null!);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "MODL000");
    }

    [Fact]
    public void Read_BufferTooSmall_ReturnsFailureMODL001()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var id = new PackageResourceId(0x01661233, 0, 1);
        var result = reader.Read(new byte[] { 1, 2 }, id);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "MODL001");
    }

    [Fact]
    public void Read_UnknownTypeId_ReturnsFailureMODL002()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var id = new PackageResourceId(0x99999999, 0, 1);
        var result = reader.Read(new byte[] { 1, 2, 3, 4 }, id);

        result.IsSuccess.Should().BeFalse();
        result.ModelKind.Should().Be(Ts3ObjectModelKind.Unknown);
        result.Issues.Should().ContainSingle(i => i.Code == "MODL002");
    }

    [Fact]
    public void Read_CompressedPayloadHeader_ReturnsFailureMODL004()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var id = new PackageResourceId(0x01661233, 0, 1);
        // RefPack compressed header 0x10, 0xFB
        byte[] compressedPayload = new byte[] { 0x10, 0xFB, 0x00, 0x42, 0x1C, 0xE0, 0x03, 0x00 };

        var result = reader.Read(compressedPayload, id);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "MODL004");
    }

    [Fact]
    public void Read_ValidModlHeader_ParsesSuccessfully_WithHeuristicReferences()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var id = new PackageResourceId(0x01661233, 0, 100);

        // Header: Version DWORD 0x00000001, then TGI block pointing to MLOD (0x01D10F34)
        byte[] payload = new byte[32];
        BitConverter.GetBytes(1u).CopyTo(payload, 0); // Version 1
        BitConverter.GetBytes(0x01D10F34u).CopyTo(payload, 4); // MLOD TypeId
        BitConverter.GetBytes(0u).CopyTo(payload, 8); // GroupId 0
        BitConverter.GetBytes(0x0000000000000100UL).CopyTo(payload, 12); // InstanceId 256

        var result = reader.Read(payload, id);

        result.IsSuccess.Should().BeTrue();
        result.ModelKind.Should().Be(Ts3ObjectModelKind.Modl);
        result.Version.Should().Be(1);
        result.LodInfos.Should().ContainSingle();
        result.LodInfos[0].AssociatedResourceId!.TypeId.Should().Be(0x01D10F34u);
    }

    [Fact]
    public void Read_ValidMlodHeader_ParsesSuccessfully_WithHeuristicGeometryCandidate()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var id = new PackageResourceId(0x01D10F34, 0, 200);

        byte[] payload = new byte[32];
        BitConverter.GetBytes(2u).CopyTo(payload, 0); // Version 2
        BitConverter.GetBytes(0x015A1849u).CopyTo(payload, 4); // TS3 GEOM TypeId
        BitConverter.GetBytes(0u).CopyTo(payload, 8); // GroupId 0
        BitConverter.GetBytes(0x0000000000000200UL).CopyTo(payload, 12); // InstanceId 512

        var result = reader.Read(payload, id);

        result.IsSuccess.Should().BeTrue();
        result.ModelKind.Should().Be(Ts3ObjectModelKind.Mlod);
        result.Version.Should().Be(2);
        result.GeometryReferences.Should().ContainSingle();
        result.GeometryReferences[0].TargetResourceId.TypeId.Should().Be(0x015A1849u);
        result.GeometryReferences[0].ReferenceType.Should().Be("HeuristicGeometryCandidate");
    }

    [Fact]
    public void Read_Stream_PreservesStreamPosition()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var id = new PackageResourceId(0x01661233, 0, 100);

        byte[] payload = new byte[32];
        BitConverter.GetBytes(1u).CopyTo(payload, 0);
        using var stream = new MemoryStream(payload);
        stream.Position = 0;

        var result = reader.Read(stream, id);

        result.IsSuccess.Should().BeTrue();
        stream.Position.Should().Be(0, "Read(Stream) must preserve initial stream position.");
    }

    [Fact]
    public async Task ReadAsync_Stream_PreservesStreamPosition()
    {
        var reader = new Ts3ObjectModelMetadataReader();
        var id = new PackageResourceId(0x01661233, 0, 100);

        byte[] payload = new byte[32];
        BitConverter.GetBytes(1u).CopyTo(payload, 0);
        using var stream = new MemoryStream(payload);
        stream.Position = 0;

        var result = await reader.ReadAsync(stream, id);

        result.IsSuccess.Should().BeTrue();
        stream.Position.Should().Be(0, "ReadAsync(Stream) must preserve initial stream position.");
    }
}
