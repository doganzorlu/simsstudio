using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;
using SimsConverter.Package.Contracts;
using Xunit;

namespace SimsConverter.Application.Tests;

public class Ts3ObjectModelDecompositionServiceTests
{
    private class FakePackageInspectionService : IPackageInspectionService
    {
        public PackageInspectionResult ResultToReturn { get; set; } = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "test.package",
            Header: null,
            Resources: Array.Empty<PackageResourceRow>(),
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private class FakePayloadReader : IPackageResourcePayloadReader
    {
        public PackageResourcePayloadResult ResultToReturn { get; set; } = new PackageResourcePayloadResult(
            IsSuccess: true,
            Payload: new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            Issues: Array.Empty<ConversionIssue>()
        );

        public PackageResourcePayloadResult ReadPayload(string packageFilePath, PackageResourceEntry entry)
        {
            return ResultToReturn;
        }

        public Task<PackageResourcePayloadResult> ReadPayloadAsync(string packageFilePath, PackageResourceEntry entry, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private class FakeMetadataReader : ITs3ObjectModelMetadataReader
    {
        public Ts3ObjectModelMetadataResult ResultToReturn { get; set; } = null!;

        public Ts3ObjectModelMetadataResult Read(ReadOnlySpan<byte> buffer, PackageResourceId resourceId)
        {
            if (ResultToReturn != null)
            {
                return ResultToReturn;
            }

            var kind = resourceId.TypeId == 0x01661233 ? Ts3ObjectModelKind.Modl : Ts3ObjectModelKind.Mlod;
            return new Ts3ObjectModelMetadataResult(
                IsSuccess: true,
                ResourceId: resourceId,
                ModelKind: kind,
                Version: 1,
                LodCount: 1,
                LodInfos: new[] { new Ts3ObjectModelLodInfo(0, 1, resourceId) },
                GeometryReferences: Array.Empty<Ts3ObjectModelGeometryReference>(),
                Issues: Array.Empty<ConversionIssue>()
            );
        }

        public Ts3ObjectModelMetadataResult Read(Stream stream, PackageResourceId resourceId)
        {
            return Read(ReadOnlySpan<byte>.Empty, resourceId);
        }

        public Task<Ts3ObjectModelMetadataResult> ReadAsync(Stream stream, PackageResourceId resourceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Read(ReadOnlySpan<byte>.Empty, resourceId));
        }
    }

    [Fact]
    public void Decompose_NullPackageInspection_ReturnsFailure()
    {
        var pkgService = new FakePackageInspectionService();
        var payloadReader = new FakePayloadReader();
        var metaReader = new FakeMetadataReader();
        var service = new Ts3ObjectModelDecompositionService(pkgService, payloadReader, metaReader);

        var result = service.Decompose((PackageInspectionResult)null!);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "DECOMP000");
    }

    [Fact]
    public void Decompose_PackageWithModlMlodRigRsltResources_CollectsAllDecompositionResources()
    {
        // Arrange
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var mlod1Entry = new PackageResourceEntry(new PackageResourceId(0x01D10F34, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);
        var mlod2Entry = new PackageResourceEntry(new PackageResourceId(0x01D10F34, 0, 3), 300, 50, 100, PackageCompressionKind.Zlib, 0);
        var rigEntry = new PackageResourceEntry(new PackageResourceId(0x8EAF13DE, 0, 4), 400, 50, 100, PackageCompressionKind.Zlib, 0);
        var rsltEntry = new PackageResourceEntry(new PackageResourceId(0xD3044521, 0, 5), 500, 50, 100, PackageCompressionKind.Zlib, 0);

        var rows = new[]
        {
            PackageResourceRow.FromEntry(modlEntry),
            PackageResourceRow.FromEntry(mlod1Entry),
            PackageResourceRow.FromEntry(mlod2Entry),
            PackageResourceRow.FromEntry(rigEntry),
            PackageResourceRow.FromEntry(rsltEntry)
        };

        var pkgResult = new PackageInspectionResult(true, "onyx_object.package", null, rows, Array.Empty<ConversionIssue>());

        var pkgService = new FakePackageInspectionService();
        var payloadReader = new FakePayloadReader();
        var metaReader = new FakeMetadataReader();
        var service = new Ts3ObjectModelDecompositionService(pkgService, payloadReader, metaReader);

        // Act
        var result = service.Decompose(pkgResult);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SourcePackagePath.Should().Be("onyx_object.package");
        result.ModlCount.Should().Be(1);
        result.MlodCount.Should().Be(2);
        result.RigCount.Should().Be(1);
        result.RsltCount.Should().Be(1);
        result.TotalModelCount.Should().Be(3);
        result.ModelMetadataResults.Should().HaveCount(3);
        result.HasDecompositionMetadata.Should().BeTrue();
    }

    [Fact]
    public void Decompose_PayloadReaderFailure_AggregatesDECOMP002Issue_AndSetsIsSuccessFalse_AndHasDecompositionMetadataFalse()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var pkgResult = new PackageInspectionResult(true, "corrupt.package", null, new[] { PackageResourceRow.FromEntry(modlEntry) }, Array.Empty<ConversionIssue>());

        var pkgService = new FakePackageInspectionService();
        var payloadReader = new FakePayloadReader
        {
            ResultToReturn = new PackageResourcePayloadResult(false, null, new[]
            {
                new ConversionIssue("PKGP004", "RefPack format not supported.", ConversionIssueSeverity.Error)
            })
        };
        var metaReader = new FakeMetadataReader();
        var service = new Ts3ObjectModelDecompositionService(pkgService, payloadReader, metaReader);

        var result = service.Decompose(pkgResult);

        result.IsSuccess.Should().BeFalse();
        result.HasDecompositionMetadata.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "DECOMP002");
        result.Issues.Should().Contain(i => i.Code == "PKGP004");
    }

    [Fact]
    public void Decompose_MetadataReaderFailure_AggregatesIssue_AndSetsIsSuccessFalse_AndHasDecompositionMetadataFalse()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var pkgResult = new PackageInspectionResult(true, "invalid_meta.package", null, new[] { PackageResourceRow.FromEntry(modlEntry) }, Array.Empty<ConversionIssue>());

        var pkgService = new FakePackageInspectionService();
        var payloadReader = new FakePayloadReader();
        var metaReader = new FakeMetadataReader
        {
            ResultToReturn = Ts3ObjectModelMetadataResult.Failure(modlEntry.Id, Ts3ObjectModelKind.Modl, "MODL003", "Corrupted header.")
        };
        var service = new Ts3ObjectModelDecompositionService(pkgService, payloadReader, metaReader);

        var result = service.Decompose(pkgResult);

        result.IsSuccess.Should().BeFalse();
        result.HasDecompositionMetadata.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MODL003");
    }

    [Fact]
    public async Task DecomposeAsync_ValidPackagePath_ExecutesDecompositionSuccessfully()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var rigEntry = new PackageResourceEntry(new PackageResourceId(0x8EAF13DE, 0, 2), 200, 50, 100, PackageCompressionKind.Zlib, 0);

        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(true, "test.package", null, new[]
            {
                PackageResourceRow.FromEntry(modlEntry),
                PackageResourceRow.FromEntry(rigEntry)
            }, Array.Empty<ConversionIssue>())
        };

        var payloadReader = new FakePayloadReader();
        var metaReader = new FakeMetadataReader();
        var service = new Ts3ObjectModelDecompositionService(pkgService, payloadReader, metaReader);

        var result = await service.DecomposeAsync("test.package");

        result.IsSuccess.Should().BeTrue();
        result.ModlCount.Should().Be(1);
        result.RigCount.Should().Be(1);
        result.ModelMetadataResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task DecomposeAsync_PayloadReaderFailure_AggregatesDECOMP002Issue_AndSetsIsSuccessFalse_AndHasDecompositionMetadataFalse()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(true, "corrupt.package", null, new[] { PackageResourceRow.FromEntry(modlEntry) }, Array.Empty<ConversionIssue>())
        };

        var payloadReader = new FakePayloadReader
        {
            ResultToReturn = new PackageResourcePayloadResult(false, null, new[]
            {
                new ConversionIssue("PKGP004", "RefPack format not supported.", ConversionIssueSeverity.Error)
            })
        };
        var metaReader = new FakeMetadataReader();
        var service = new Ts3ObjectModelDecompositionService(pkgService, payloadReader, metaReader);

        var result = await service.DecomposeAsync("corrupt.package");

        result.IsSuccess.Should().BeFalse();
        result.HasDecompositionMetadata.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "DECOMP002");
        result.Issues.Should().Contain(i => i.Code == "PKGP004");
    }

    [Fact]
    public async Task DecomposeAsync_MetadataReaderFailure_AggregatesIssue_AndSetsIsSuccessFalse_AndHasDecompositionMetadataFalse()
    {
        var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = new PackageInspectionResult(true, "invalid_meta.package", null, new[] { PackageResourceRow.FromEntry(modlEntry) }, Array.Empty<ConversionIssue>())
        };

        var payloadReader = new FakePayloadReader();
        var metaReader = new FakeMetadataReader
        {
            ResultToReturn = Ts3ObjectModelMetadataResult.Failure(modlEntry.Id, Ts3ObjectModelKind.Modl, "MODL003", "Corrupted header.")
        };
        var service = new Ts3ObjectModelDecompositionService(pkgService, payloadReader, metaReader);

        var result = await service.DecomposeAsync("invalid_meta.package");

        result.IsSuccess.Should().BeFalse();
        result.HasDecompositionMetadata.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MODL003");
    }
}
