using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using Xunit;

namespace SimsConverter.Application.Tests;

public class Ts4ResourcePayloadCompatibilityVerifierTests
{
    [Fact]
    public void VerifyPackagePayloads_WhenPackageFileDoesNotExist_ReturnsFailureWithVAL000()
    {
        var verifier = new Ts4ResourcePayloadCompatibilityVerifier();
        var result = verifier.VerifyPackagePayloads("non_existent.package", new DbpfParseResult(true, null, Array.Empty<PackageResourceEntry>(), Array.Empty<ConversionIssue>()));

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "VAL000");
    }

    [Fact]
    public void VerifyPackagePayloads_WhenCobjReferencesNonExistentModl_ReturnsFailureWithVAL001()
    {
        var verifier = new Ts4ResourcePayloadCompatibilityVerifier();
        var parser = new DbpfPackageParser();
        var writer = new DecorativeObjectPackageWriter();

        string tempDir = Path.Combine(Path.GetTempPath(), "ts4_val001_test_" + Guid.NewGuid().ToString("N"));
        string pkgPath = Path.Combine(tempDir, "invalid_cobj.package");

        try
        {
            Directory.CreateDirectory(tempDir);

            // COBJ pointing to non-existent MODL instance 99999
            byte[] badCobjPayload = new byte[24];
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("COBJ"), 0, badCobjPayload, 0, 4);
            BitConverter.GetBytes((uint)1).CopyTo(badCobjPayload, 4); // version
            BitConverter.GetBytes(Ts4ResourceTypeIds.Model).CopyTo(badCobjPayload, 8); // TypeId
            BitConverter.GetBytes((uint)0).CopyTo(badCobjPayload, 12); // GroupId
            BitConverter.GetBytes((ulong)99999).CopyTo(badCobjPayload, 16); // InstanceId (non-existent)

            var badCobjEntry = new DecorativeObjectPackageWriteResourceEntry(
                ResourceId: new PackageResourceId(Ts4ResourceTypeIds.CatalogObject, 0, 1),
                FormattedKey: "319E4F1D:00000000:0000000000000001",
                Payload: badCobjPayload,
                CompressionKind: Domain.Enums.PackageCompressionKind.None,
                DecompressedSize: 24,
                CompressedSize: 24
            );

            var plan = new DecorativeObjectPackageWritePlan(
                SourcePackagePath: "source.package",
                TargetOutputPath: pkgPath,
                TargetGameVersion: Domain.Enums.GameVersion.Sims4,
                PlannedResources: new[] { badCobjEntry },
                IsPlanValid: true,
                Issues: Array.Empty<ConversionIssue>()
            );

            var writeResult = writer.WritePackage(plan);
            writeResult.IsSuccess.Should().BeTrue();

            byte[] bytes = File.ReadAllBytes(pkgPath);
            var parseResult = parser.Parse(bytes);
            parseResult.IsSuccess.Should().BeTrue();

            var result = verifier.VerifyPackagePayloads(pkgPath, parseResult);
            result.IsSuccess.Should().BeFalse("COBJ referencing non-existent MODL must fail payload compatibility verification.");
            result.Issues.Should().Contain(i => i.Code == "VAL001");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void VerifyPackagePayloads_MultipleMeshesInMlod_ValidatesAllGeomAndMaterialLinks()
    {
        var verifier = new Ts4ResourcePayloadCompatibilityVerifier();
        var parser = new DbpfPackageParser();
        var writer = new DecorativeObjectPackageWriter();

        string tempDir = Path.Combine(Path.GetTempPath(), "ts4_mlod_multimesh_test_" + Guid.NewGuid().ToString("N"));
        string pkgPath = Path.Combine(tempDir, "multimesh_mlod.package");

        try
        {
            Directory.CreateDirectory(tempDir);

            var geomId1 = new PackageResourceId(Ts4ResourceTypeIds.Geom, 0, 101);
            var geomId2 = new PackageResourceId(Ts4ResourceTypeIds.Geom, 0, 102);
            var matId = new PackageResourceId(Ts4ResourceTypeIds.MaterialDefinition, 0, 201);
            var mlodId = new PackageResourceId(Ts4ResourceTypeIds.ModelLod, 0, 301);

            // MLOD canonical layout:
            // 0x00: "MLOD"
            // 0x04: version 1
            // 0x08: lodIndex 0
            // 0x0C: meshCount 2
            // 0x10: GEOM1 (16B)
            // 0x20: GEOM2 (16B)
            // 0x30: Material (16B)
            byte[] mlodPayload = new byte[16 + 16 + 16 + 16]; // 64 bytes total
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("MLOD"), 0, mlodPayload, 0, 4);
            BitConverter.GetBytes((uint)1).CopyTo(mlodPayload, 4); // version
            BitConverter.GetBytes((uint)0).CopyTo(mlodPayload, 8); // lodIndex 0
            BitConverter.GetBytes((uint)2).CopyTo(mlodPayload, 12); // meshCount 2

            // GEOM1 at 0x10
            BitConverter.GetBytes(geomId1.TypeId).CopyTo(mlodPayload, 16);
            BitConverter.GetBytes(geomId1.GroupId).CopyTo(mlodPayload, 20);
            BitConverter.GetBytes(geomId1.InstanceId).CopyTo(mlodPayload, 24);

            // GEOM2 at 0x20
            BitConverter.GetBytes(geomId2.TypeId).CopyTo(mlodPayload, 32);
            BitConverter.GetBytes(geomId2.GroupId).CopyTo(mlodPayload, 36);
            BitConverter.GetBytes(geomId2.InstanceId).CopyTo(mlodPayload, 40);

            // Material at 0x30
            BitConverter.GetBytes(matId.TypeId).CopyTo(mlodPayload, 48);
            BitConverter.GetBytes(matId.GroupId).CopyTo(mlodPayload, 52);
            BitConverter.GetBytes(matId.InstanceId).CopyTo(mlodPayload, 56);

            byte[] validGeomBytes = CreateValidTs4GeomBytes();
            uint geomSize = (uint)validGeomBytes.Length;
            byte[] matPayload = CreateValidRmatBytes();
            uint matSize = (uint)matPayload.Length;

            var mlodEntry = new DecorativeObjectPackageWriteResourceEntry(mlodId, mlodId.FormattedKey, mlodPayload, Domain.Enums.PackageCompressionKind.None, 64, 64);
            var geomEntry1 = new DecorativeObjectPackageWriteResourceEntry(geomId1, geomId1.FormattedKey, validGeomBytes, Domain.Enums.PackageCompressionKind.None, geomSize, geomSize);
            var geomEntry2 = new DecorativeObjectPackageWriteResourceEntry(geomId2, geomId2.FormattedKey, validGeomBytes, Domain.Enums.PackageCompressionKind.None, geomSize, geomSize);
            var matEntry = new DecorativeObjectPackageWriteResourceEntry(matId, matId.FormattedKey, matPayload, Domain.Enums.PackageCompressionKind.None, matSize, matSize);

            var plan = new DecorativeObjectPackageWritePlan(
                SourcePackagePath: "source.package",
                TargetOutputPath: pkgPath,
                TargetGameVersion: Domain.Enums.GameVersion.Sims4,
                PlannedResources: new[] { mlodEntry, geomEntry1, geomEntry2, matEntry },
                IsPlanValid: true,
                Issues: Array.Empty<ConversionIssue>()
            );

            var writeResult = writer.WritePackage(plan);
            writeResult.IsSuccess.Should().BeTrue();

            byte[] bytes = File.ReadAllBytes(pkgPath);
            var parseResult = parser.Parse(bytes);
            parseResult.IsSuccess.Should().BeTrue();

            var result = verifier.VerifyPackagePayloads(pkgPath, parseResult);
            result.IsSuccess.Should().BeTrue("Multi-mesh MLOD with verified GEOM and Material TGIs must pass verification.");
            result.VerifiedTgiLinkCount.Should().Be(3, "All 2 GEOM links + 1 Material link must be counted.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void VerifyPackagePayloads_WhenMlodPayloadIsTruncated_EmitsVAL003Error()
    {
        var verifier = new Ts4ResourcePayloadCompatibilityVerifier();
        var parser = new DbpfPackageParser();
        var writer = new DecorativeObjectPackageWriter();

        string tempDir = Path.Combine(Path.GetTempPath(), "ts4_mlod_truncated_test_" + Guid.NewGuid().ToString("N"));
        string pkgPath = Path.Combine(tempDir, "truncated_mlod.package");

        try
        {
            Directory.CreateDirectory(tempDir);

            var mlodId = new PackageResourceId(Ts4ResourceTypeIds.ModelLod, 0, 301);

            // MLOD payload declaring meshCount = 2, but truncated to 20 bytes (only space for half a mesh TGI)
            byte[] truncatedMlodPayload = new byte[20];
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("MLOD"), 0, truncatedMlodPayload, 0, 4);
            BitConverter.GetBytes((uint)1).CopyTo(truncatedMlodPayload, 4);
            BitConverter.GetBytes((uint)0).CopyTo(truncatedMlodPayload, 8);
            BitConverter.GetBytes((uint)2).CopyTo(truncatedMlodPayload, 12); // meshCount = 2 requires 16 + 32 = 48 bytes!

            var mlodEntry = new DecorativeObjectPackageWriteResourceEntry(mlodId, mlodId.FormattedKey, truncatedMlodPayload, Domain.Enums.PackageCompressionKind.None, 20, 20);

            var plan = new DecorativeObjectPackageWritePlan(
                SourcePackagePath: "source.package",
                TargetOutputPath: pkgPath,
                TargetGameVersion: Domain.Enums.GameVersion.Sims4,
                PlannedResources: new[] { mlodEntry },
                IsPlanValid: true,
                Issues: Array.Empty<ConversionIssue>()
            );

            var writeResult = writer.WritePackage(plan);
            writeResult.IsSuccess.Should().BeTrue();

            byte[] bytes = File.ReadAllBytes(pkgPath);
            var parseResult = parser.Parse(bytes);

            var result = verifier.VerifyPackagePayloads(pkgPath, parseResult);
            result.IsSuccess.Should().BeFalse("Truncated MLOD payload must fail verification.");
            result.Issues.Should().Contain(i => i.Code == "VAL003", "VAL003 error issue must be emitted for truncated MLOD payload.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void VerifyPackagePayloads_WhenMlodMeshCountIsInvalid_EmitsVAL003Error()
    {
        var verifier = new Ts4ResourcePayloadCompatibilityVerifier();
        var parser = new DbpfPackageParser();
        var writer = new DecorativeObjectPackageWriter();

        string tempDir = Path.Combine(Path.GetTempPath(), "ts4_mlod_invalid_count_test_" + Guid.NewGuid().ToString("N"));
        string pkgPath = Path.Combine(tempDir, "invalid_count_mlod.package");

        try
        {
            Directory.CreateDirectory(tempDir);

            var mlodId = new PackageResourceId(Ts4ResourceTypeIds.ModelLod, 0, 301);

            // MLOD payload declaring meshCount = 99999, but payload size is only 32 bytes
            byte[] invalidCountMlodPayload = new byte[32];
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("MLOD"), 0, invalidCountMlodPayload, 0, 4);
            BitConverter.GetBytes((uint)1).CopyTo(invalidCountMlodPayload, 4);
            BitConverter.GetBytes((uint)0).CopyTo(invalidCountMlodPayload, 8);
            BitConverter.GetBytes((uint)99999).CopyTo(invalidCountMlodPayload, 12); // Exceeds payload size

            var mlodEntry = new DecorativeObjectPackageWriteResourceEntry(mlodId, mlodId.FormattedKey, invalidCountMlodPayload, Domain.Enums.PackageCompressionKind.None, 32, 32);

            var plan = new DecorativeObjectPackageWritePlan(
                SourcePackagePath: "source.package",
                TargetOutputPath: pkgPath,
                TargetGameVersion: Domain.Enums.GameVersion.Sims4,
                PlannedResources: new[] { mlodEntry },
                IsPlanValid: true,
                Issues: Array.Empty<ConversionIssue>()
            );

            var writeResult = writer.WritePackage(plan);
            writeResult.IsSuccess.Should().BeTrue();

            byte[] bytes = File.ReadAllBytes(pkgPath);
            var parseResult = parser.Parse(bytes);

            var result = verifier.VerifyPackagePayloads(pkgPath, parseResult);
            result.IsSuccess.Should().BeFalse("MLOD payload with meshCount exceeding payload size must fail verification.");
            result.Issues.Should().Contain(i => i.Code == "VAL003");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void VerifyPackagePayloads_WhenMlodReferencesNonExistentGeomOrMaterial_EmitsVAL003Error()
    {
        var verifier = new Ts4ResourcePayloadCompatibilityVerifier();
        var parser = new DbpfPackageParser();
        var writer = new DecorativeObjectPackageWriter();

        string tempDir = Path.Combine(Path.GetTempPath(), "ts4_mlod_non_existent_ref_" + Guid.NewGuid().ToString("N"));
        string pkgPath = Path.Combine(tempDir, "non_existent_ref_mlod.package");

        try
        {
            Directory.CreateDirectory(tempDir);

            var mlodId = new PackageResourceId(Ts4ResourceTypeIds.ModelLod, 0, 301);

            // MLOD referencing non-existent GEOM instance 88888
            byte[] mlodPayload = new byte[48];
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("MLOD"), 0, mlodPayload, 0, 4);
            BitConverter.GetBytes((uint)1).CopyTo(mlodPayload, 4);
            BitConverter.GetBytes((uint)0).CopyTo(mlodPayload, 8);
            BitConverter.GetBytes((uint)1).CopyTo(mlodPayload, 12); // meshCount = 1

            // Non-existent GEOM TGI
            BitConverter.GetBytes(Ts4ResourceTypeIds.Geom).CopyTo(mlodPayload, 16);
            BitConverter.GetBytes((uint)0).CopyTo(mlodPayload, 20);
            BitConverter.GetBytes((ulong)88888).CopyTo(mlodPayload, 24);

            // Non-existent Material TGI
            BitConverter.GetBytes(Ts4ResourceTypeIds.MaterialDefinition).CopyTo(mlodPayload, 32);
            BitConverter.GetBytes((uint)0).CopyTo(mlodPayload, 36);
            BitConverter.GetBytes((ulong)77777).CopyTo(mlodPayload, 40);

            var mlodEntry = new DecorativeObjectPackageWriteResourceEntry(mlodId, mlodId.FormattedKey, mlodPayload, Domain.Enums.PackageCompressionKind.None, 48, 48);

            var plan = new DecorativeObjectPackageWritePlan(
                SourcePackagePath: "source.package",
                TargetOutputPath: pkgPath,
                TargetGameVersion: Domain.Enums.GameVersion.Sims4,
                PlannedResources: new[] { mlodEntry },
                IsPlanValid: true,
                Issues: Array.Empty<ConversionIssue>()
            );

            writer.WritePackage(plan);
            byte[] bytes = File.ReadAllBytes(pkgPath);
            var parseResult = parser.Parse(bytes);

            var result = verifier.VerifyPackagePayloads(pkgPath, parseResult);
            result.IsSuccess.Should().BeFalse("MLOD referencing non-existent GEOM/Material must fail verification.");
            result.Issues.Should().Contain(i => i.Code == "VAL003");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static byte[] CreateValidRmatBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RMAT"));
        bw.Write((uint)1);
        bw.Write((uint)0); // 0 textures
        return ms.ToArray();
    }

    private static byte[] CreateValidTs4GeomBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Header (20B)
        bw.Write(System.Text.Encoding.ASCII.GetBytes("GEOM"));
        bw.Write((uint)12); // Version 12
        bw.Write((uint)0);  // TGI offset
        bw.Write((uint)0);  // TGI size
        bw.Write((uint)0);  // Shader hash (0 = no MTNF block)

        // Block header (16B)
        bw.Write((int)0); // mergeGroup
        bw.Write((int)0); // sortOrder
        bw.Write((uint)3); // vertexCount (3)
        bw.Write((uint)1); // elementCount (1 = Position only)

        // Element descriptor (9B)
        bw.Write((uint)1); // datatype = Position
        bw.Write((uint)3); // format = float3
        bw.Write((byte)12); // size = 12B

        // Vertex buffer (3 verts * 12B = 36B)
        for (int i = 0; i < 3; i++)
        {
            bw.Write(0.0f); bw.Write(0.0f); bw.Write(0.0f);
        }

        // Submesh count (4B)
        bw.Write((uint)1); // numSubMeshes = 1

        // Submesh 0: bytesPerFacePoint (1B), numFacePoints (4B)
        bw.Write((byte)2); // 2 bytes per point (UInt16)
        bw.Write((uint)3); // 3 face points (1 triangle)
        bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)2);

        // Stitches (12B for version 12)
        bw.Write((uint)0); // uvStitchCount
        bw.Write((uint)0); // slotrayCount

        // Bone section
        bw.Write((uint)0); // boneCount

        // Tail TGI
        bw.Write((uint)0);

        return ms.ToArray();
    }
}
