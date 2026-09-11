using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using Xunit;

namespace SimsConverter.Application.Tests;

public class Ts3CatalogMetadataReaderTests
{
    private class FakePayloadReader : Package.Contracts.IPackageResourcePayloadReader
    {
        public byte[]? PayloadToReturn { get; set; }
        public PackageResourcePayloadResult ReadPayload(string packageFilePath, PackageResourceEntry entry) =>
            new PackageResourcePayloadResult(PayloadToReturn != null, PayloadToReturn, Array.Empty<ConversionIssue>());

        public Task<PackageResourcePayloadResult> ReadPayloadAsync(string packageFilePath, PackageResourceEntry entry, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(ReadPayload(packageFilePath, entry));
    }

    [Fact]
    public void ReadCatalogMetadata_WhenObjdPresent_ParsesPricePlacementAndFootprint()
    {
        var fakeReader = new FakePayloadReader();
        var reader = new Ts3CatalogMetadataReader(fakeReader);

        // Build valid OBJD payload:
        // 'OBJD' (4) + Version (4) + Price: 250 (4) + CatalogGroup: 12 (4) + PlacementFlags: 0x00000004 (4) + FootprintHash: 0x12345678 (4)
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
        writer.Write(Encoding.ASCII.GetBytes("OBJD"));
        writer.Write((uint)1);
        writer.Write((uint)250);          // Price
        writer.Write((uint)12);           // CatalogGroup
        writer.Write((uint)0x00000004);   // PlacementFlags
        writer.Write((uint)0x12345678);   // FootprintHash

        fakeReader.PayloadToReturn = ms.ToArray();

        var objdRow = PackageResourceRow.FromEntry(new PackageResourceEntry(new PackageResourceId(0x319E4F1D, 0, 0x100), 0, 100, 100, PackageCompressionKind.None, 0));
        var resources = new[] { objdRow };

        var metadata = reader.ReadCatalogMetadata("test.package", resources, "CustomChair");

        metadata.Should().NotBeNull();
        metadata.Price.Should().Be(250);
        metadata.CatalogGroup.Should().Be(12);
        metadata.PlacementFlags.Should().Be(0x00000004);
        metadata.FootprintHash.Should().Be(0x12345678);
        metadata.ObjectTitle.Should().Be("CustomChair");
        metadata.IsDefaultFallback.Should().BeFalse();
    }

    [Fact]
    public void ReadCatalogMetadata_WhenObjdMissing_ReturnsControlledFallback()
    {
        var reader = new Ts3CatalogMetadataReader();
        var metadata = reader.ReadCatalogMetadata("test.package", Array.Empty<PackageResourceRow>(), "FallbackObject");

        metadata.Should().NotBeNull();
        metadata.Price.Should().Be(100);
        metadata.PlacementFlags.Should().Be(0x00000001);
        metadata.FootprintHash.Should().Be(0x00000000);
        metadata.IsDefaultFallback.Should().BeTrue();
    }
}
