using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Models;
using Xunit;

namespace SimsConverter.App.Tests;

public class ResourceInspectorViewModelCapabilityTests
{
    private static PackageResourceRow CreateRow(uint typeId, ulong instanceId) =>
        PackageResourceRow.FromEntry(new PackageResourceEntry(new PackageResourceId(typeId, 0, instanceId), 0, 100, 100, PackageCompressionKind.None, 0));

    [Fact]
    public void UpdateCapabilityMatrixPreflight_GivenValidPackageResult_PopulatesCapabilitySurfaceAndCounts()
    {
        // Arrange
        var mockInspectionService = new StubPackageInspectionService();
        var capabilityService = new DecorativeObjectConversionCapabilityService();
        var vm = new ResourceInspectorViewModel(mockInspectionService, capabilityService: capabilityService);

        var resources = new List<PackageResourceRow>
        {
            CreateRow(Ts4ResourceTypeIds.CatalogObject, 1),
            CreateRow(Ts4ResourceTypeIds.ObjectDefinition, 2),
            CreateRow(Ts4ResourceTypeIds.Model, 3),
            CreateRow(0x220557DA, 4), // STBL (PassThrough)
            CreateRow(0x03B33DDF, 5)  // ITUN (Unsupported CAPA001)
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "sample.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        vm.UpdateCapabilityMatrixPreflight(packageResult);

        // Assert
        vm.HasCapabilityMatrix.Should().BeTrue();
        vm.CapabilitySupportedCount.Should().Be(3);
        vm.CapabilityPassThroughCount.Should().Be(1);
        vm.CapabilityUnsupportedCount.Should().Be(1);
        vm.CapabilityEntries.Should().HaveCount(5);

        vm.HasCapabilityWarnings.Should().BeTrue();
        vm.CapabilityWarnings.Should().ContainSingle(w => w.Code == "CAPA001" && w.Message.Contains("ITUN"));
    }

    [Fact]
    public void CanConvert_GivenPackageWithZeroSupportedResources_ReturnsFalse()
    {
        // Arrange: Package containing only 1 unknown resource (0 supported object model/definition/mesh resources)
        var mockInspectionService = new StubPackageInspectionService();
        var capabilityService = new DecorativeObjectConversionCapabilityService();
        var vm = new ResourceInspectorViewModel(mockInspectionService, capabilityService: capabilityService)
        {
            SelectedFilePath = "corrupt.package",
            TargetOutputPath = "output_ts4.package"
        };

        var resources = new List<PackageResourceRow>
        {
            CreateRow(0x99998888, 1) // Unknown type
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "corrupt.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        vm.UpdateCapabilityMatrixPreflight(packageResult);

        // Assert
        vm.HasCapabilityMatrix.Should().BeTrue();
        vm.CapabilitySupportedCount.Should().Be(0);
        vm.CapabilityUnsupportedCount.Should().Be(1);

        // Guard Check: CanConvert must return false when 0 supported resources exist!
        vm.CanConvert.Should().BeFalse("Conversion must be disabled when 0 supported object model/mesh/definition resources exist.");
    }

    private sealed class StubPackageInspectionService : IPackageInspectionService
    {
        public Task<PackageInspectionResult> InspectFileAsync(string filePath, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PackageInspectionResult.Failure(filePath, "STUB", "Stub service"));
        }
    }
}
