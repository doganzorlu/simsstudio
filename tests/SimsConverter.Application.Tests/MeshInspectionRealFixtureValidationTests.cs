using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Application.Tests;

public class MeshInspectionRealFixtureValidationTests
{
    private readonly MeshInspectionService _service;

    public MeshInspectionRealFixtureValidationTests()
    {
        IDbpfPackageParser parser = new DbpfPackageParser();
        var packageService = new PackageInspectionService(parser);
        var classifier = new MeshResourceClassifier();
        var ts3Importer = new Ts3GeomCanonicalMeshImporter();
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), new CanonicalMeshValidator());
        var payloadReader = new PackageResourcePayloadReader();

        _service = new MeshInspectionService(packageService, classifier, ts3Importer, ts4Importer, payloadReader);
    }

    private static string GetLocalMeshFixtureDirectory()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(solutionDir, "fixtures", "local", "mesh");
    }

    [Fact]
    public async Task InspectPackageMeshesAsync_WhenLocalPackageFixturesPresent_ValidatesEndToEndPipelineCleanly()
    {
        // Arrange
        string fixtureDir = GetLocalMeshFixtureDirectory();
        if (!Directory.Exists(fixtureDir))
        {
            Directory.CreateDirectory(fixtureDir);
        }

        string[] packageFiles = Directory.GetFiles(fixtureDir, "*.package", SearchOption.AllDirectories);

        if (packageFiles.Length == 0)
        {
            // Clean no-op / skip behavior when local sample .package files are absent
            Assert.True(true, "No local .package mesh fixture files found in fixtures/local/mesh/ - test skipped cleanly without failure.");
            return;
        }

        // Act & Assert for package fixtures
        foreach (var file in packageFiles)
        {
            var request = new MeshInspectionRequest(PackageFilePath: file);
            var result = await _service.InspectPackageMeshesAsync(request);

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue($"Package inspection of fixture '{Path.GetFileName(file)}' must succeed.");
            result.Rows.Should().NotBeNull();

            foreach (var row in result.Rows.Where(r => r.ClassificationKind == MeshClassificationKind.KnownMesh))
            {
                row.CanExtractRawPayload.Should().BeTrue();
                if (row.CanInspectCanonicalMesh)
                {
                    row.VertexCount.Should().NotBeNull();
                    row.VertexCount!.Value.Should().BeGreaterThan(0u);
                    row.FaceCount.Should().NotBeNull();
                    row.FaceCount!.Value.Should().BeGreaterThan(0u);
                }
            }
        }
    }
}
