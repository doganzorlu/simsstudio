using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class Ts4ToTs3ReverseConversionValidationTests
{
    private readonly ITestOutputHelper _output;

    public Ts4ToTs3ReverseConversionValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static byte[] CreateMinimalValidGeomPayload()
    {
        uint headerSize = 8;
        int geomChunkOffset = 48;
        uint geomVersion = 5;

        int vertexCount = 3;
        int facePointCount = 3;
        byte bytesPerFacePoint = 2;
        int boneCount = 1;

        var descriptors = new List<(uint datatype, uint format, byte size)>
        {
            (1, 3, 12), // Position float3 (12B)
            (2, 3, 12), // Normal float3 (12B)
            (3, 2, 8),  // UV0 float2 (8B)
            (4, 4, 4),  // Bone Indices byte4 (4B)
            (5, 2, 4)   // Bone Weights byte4 (4B)
        };

        int elementCount = descriptors.Count;
        int strideBytes = 0;
        foreach (var desc in descriptors) strideBytes += desc.size;

        int vertexBufferBytes = vertexCount * strideBytes;
        int indexBufferBytes = facePointCount * bytesPerFacePoint;
        int boneHashBytes = boneCount * 4;
        int submeshBytes = 4 + 1 + 4 + indexBufferBytes;
        int skinControllerOrStitchesBytes = 4;
        int boneSectionBytes = 4 + boneHashBytes;
        int tailTgiBytes = 4 + 16;

        int geomChunkLength = 20 + 16 + (elementCount * 9) + vertexBufferBytes + submeshBytes + skinControllerOrStitchesBytes + boneSectionBytes + tailTgiBytes;
        int tailStartPos = geomChunkLength - tailTgiBytes;
        uint rawTgiOffset = (uint)(tailStartPos - 12);
        uint tgiSize = (uint)tailTgiBytes;

        int totalSizeBytes = geomChunkOffset + geomChunkLength;
        var buffer = new byte[totalSizeBytes];
        var span = buffer.AsSpan();

        // 1. Count-First RCOL Header
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1);

        // 2. Internal ITG at offset 8 (16B)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), 0x5555666677778888UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x015A1849); // GEOM TypeId
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0x00000000);

        // 3. External TGI at offset 24 (16B)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x015A182C);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), (uint)geomChunkOffset);

        // 4. Chunk Location Table at offset 40 (8B)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)(geomChunkOffset - (int)headerSize));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), (uint)geomChunkLength);

        // 5. GEOM Chunk at offset 48
        var geomSpan = span.Slice(geomChunkOffset);
        Encoding.ASCII.GetBytes("GEOM", geomSpan.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(4, 4), geomVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(8, 4), rawTgiOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(12, 4), tgiSize);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0);

        int curr = 20;
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 8, 4), (uint)vertexCount);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), (uint)elementCount);
        curr += 16;

        foreach (var desc in descriptors)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), desc.datatype);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), desc.format);
            geomSpan[curr + 8] = desc.size;
            curr += 9;
        }

        // 3 Vertices (3 * 40B = 120B)
        for (int v = 0; v < vertexCount; v++)
        {
            int vStart = curr + (v * strideBytes);
            int ePos = 0;
            foreach (var desc in descriptors)
            {
                if (desc.datatype == 1)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos, 4), (float)(v * 1.0 + 1.0));
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 4, 4), (float)(v * 2.0 + 1.0));
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 8, 4), (float)(v * 3.0 + 1.0));
                }
                else if (desc.datatype == 2)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos, 4), 0.0f);
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 4, 4), 1.0f);
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 8, 4), 0.0f);
                }
                else if (desc.datatype == 3)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos, 4), 0.25f);
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 4, 4), 0.75f);
                }
                else if (desc.datatype == 4)
                {
                    geomSpan[vStart + ePos] = 0;
                    geomSpan[vStart + ePos + 1] = 0;
                    geomSpan[vStart + ePos + 2] = 0;
                    geomSpan[vStart + ePos + 3] = 0;
                }
                else if (desc.datatype == 5)
                {
                    geomSpan[vStart + ePos] = 255;
                    geomSpan[vStart + ePos + 1] = 0;
                    geomSpan[vStart + ePos + 2] = 0;
                    geomSpan[vStart + ePos + 3] = 0;
                }
                ePos += desc.size;
            }
        }
        curr += vertexBufferBytes;

        // Submesh Section
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        geomSpan[curr + 4] = bytesPerFacePoint;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 5, 4), (uint)facePointCount);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 9, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 11, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 13, 2), 2);
        curr += submeshBytes;

        // Stitches / SkinController
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;

        // Bone Section
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)boneCount);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 0x12345678);
        curr += boneSectionBytes;

        // Embedded TGI Tail
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(geomSpan.Slice(curr + 4, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), 0x015A182C);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 16, 4), 0x00000000);

        return buffer;
    }

    private static string CreateSyntheticTs3PackageFile(string tempDir, string filename = "source_ts3.package")
    {
        string dummySourcePath = Path.Combine(tempDir, "dummy_" + Guid.NewGuid().ToString("N") + ".package");
        string path = Path.Combine(tempDir, filename);
        var writer = new DbpfPackageWriter();

        var geomPayload = CreateMinimalValidGeomPayload();
        var geomId = new PackageResourceId(0x015A1849, 0x00000000, 0x1122334455667788UL);

        var entries = new List<DbpfPackageWriteResourceEntry>
        {
            new DbpfPackageWriteResourceEntry(geomId, geomPayload, PackageCompressionKind.None, (uint)geomPayload.Length)
        };

        writer.WritePackage(dummySourcePath, path, entries);
        return path;
    }

    private static string CreateSyntheticTs4PackageFile(string tempDir, string filename = "source_ts4.package")
    {
        string dummySourcePath = Path.Combine(tempDir, "dummy_" + Guid.NewGuid().ToString("N") + ".package");
        string path = Path.Combine(tempDir, filename);
        var writer = new DbpfPackageWriter();

        var geomPayload = CreateMinimalValidGeomPayload();
        var geomId = new PackageResourceId(0x015A1849, 0x00000000, 0x9988776655443322UL);

        // COBJ payload (24 bytes)
        var cobjMs = new MemoryStream();
        using (var bw = new BinaryWriter(cobjMs, Encoding.UTF8, true))
        {
            bw.Write(Encoding.ASCII.GetBytes("COBJ"));
            bw.Write((uint)1);
            bw.Write((uint)0x01661233); // MODL TypeId
            bw.Write((uint)0x00000000); // MODL GroupId
            bw.Write((ulong)0x1234567812345678UL); // MODL InstanceId
        }
        var cobjPayload = cobjMs.ToArray();
        var cobjId = new PackageResourceId(0x319E4F1D, 0x00000000, 0x1234567812345678UL);

        // MODL payload
        var modlMs = new MemoryStream();
        using (var bw = new BinaryWriter(modlMs, Encoding.UTF8, true))
        {
            bw.Write(Encoding.ASCII.GetBytes("MODL"));
            bw.Write((uint)1);
            bw.Write((uint)0x01D10F34);
            bw.Write((uint)0x00000000);
            bw.Write((ulong)0x8765432187654321UL);
        }
        var modlPayload = modlMs.ToArray();
        var modlId = new PackageResourceId(0x01661233, 0x00000000, 0x1234567812345678UL);

        // MLOD payload
        var mlodMs = new MemoryStream();
        using (var bw = new BinaryWriter(mlodMs, Encoding.UTF8, true))
        {
            bw.Write(Encoding.ASCII.GetBytes("MLOD"));
            bw.Write((uint)1);
            bw.Write((uint)0); // LOD0
            bw.Write((uint)1); // 1 GEOM
            bw.Write(geomId.TypeId);
            bw.Write(geomId.GroupId);
            bw.Write(geomId.InstanceId);
            // Material TGI
            bw.Write((uint)0x015A182C);
            bw.Write((uint)0x00000000);
            bw.Write((ulong)0x1111222233334444UL);
        }
        var mlodPayload = mlodMs.ToArray();
        var mlodId = new PackageResourceId(0x01D10F34, 0x00000000, 0x8765432187654321UL);

        var entries = new List<DbpfPackageWriteResourceEntry>
        {
            new DbpfPackageWriteResourceEntry(cobjId, cobjPayload, PackageCompressionKind.None, (uint)cobjPayload.Length),
            new DbpfPackageWriteResourceEntry(modlId, modlPayload, PackageCompressionKind.None, (uint)modlPayload.Length),
            new DbpfPackageWriteResourceEntry(mlodId, mlodPayload, PackageCompressionKind.None, (uint)mlodPayload.Length),
            new DbpfPackageWriteResourceEntry(geomId, geomPayload, PackageCompressionKind.None, (uint)geomPayload.Length)
        };

        writer.WritePackage(dummySourcePath, path, entries);
        return path;
    }

    private static DecorativeObjectConversionService CreateService()
    {
        var dbpfParser = new DbpfPackageParser();
        var pkgInspector = new PackageInspectionService(dbpfParser);
        var validator = new CanonicalMeshValidator();
        var classifier = new MeshResourceClassifier();
        var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
        var payloadReader = new PackageResourcePayloadReader();

        var meshInspector = new MeshInspectionService(pkgInspector, classifier, ts3Importer, ts4Importer, payloadReader);
        var texInspector = new TextureInspectionService(pkgInspector, new TextureResourceClassifier());
        var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(pkgInspector, meshInspector, texInspector);
        var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, validator, ts4Importer);
        var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader);
        var packageWriter = new DecorativeObjectPackageWriter();
        var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

        return new DecorativeObjectConversionService(
            pkgInspector,
            meshInspector,
            texInspector,
            sourceGraphBuilder,
            inputBundleBuilder,
            writePlanBuilder,
            packageWriter,
            payloadVerifier,
            dbpfParser
        );
    }

    [Fact]
    public async Task ExecuteConversion_WhenTargetGameVersionIsSims3_ConvertsTs4ToTs3_AndGeneratesTs3Resources()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_014_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string ts4SourcePath = CreateSyntheticTs4PackageFile(tempDir);
            string ts3TargetPath = Path.Combine(tempDir, "converted_ts3.package");

            var service = CreateService();
            var request = new DecorativeObjectConversionRequest(
                SourcePackagePath: ts4SourcePath,
                TargetOutputPath: ts3TargetPath,
                TargetGameVersion: GameVersion.Sims3
            );

            var result = await service.ExecuteConversionAsync(request);

            result.IsSuccess.Should().BeTrue();
            File.Exists(ts3TargetPath).Should().BeTrue();

            var dbpfParser = new DbpfPackageParser();
            var parseResult = await dbpfParser.ParseFileAsync(ts3TargetPath);

            parseResult.IsSuccess.Should().BeTrue();
            parseResult.Entries.Should().NotBeEmpty();

            // Verify TS3 target MODL, MLOD, GEOM exist in output package index
            parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x01661233); // MODL
            parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x01D10F34); // MLOD
            parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x015A1849); // GEOM
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteConversion_Ts4ToTs3_ProducesDeterministicResourceIdentities()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_014_det_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string ts4SourcePath = CreateSyntheticTs4PackageFile(tempDir);
            string targetPath1 = Path.Combine(tempDir, "ts3_output1.package");
            string targetPath2 = Path.Combine(tempDir, "ts3_output2.package");

            var service = CreateService();
            var request1 = new DecorativeObjectConversionRequest(ts4SourcePath, targetPath1, GameVersion.Sims3);
            var request2 = new DecorativeObjectConversionRequest(ts4SourcePath, targetPath2, GameVersion.Sims3);

            var result1 = await service.ExecuteConversionAsync(request1);
            var result2 = await service.ExecuteConversionAsync(request2);

            result1.IsSuccess.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();

            byte[] bytes1 = File.ReadAllBytes(targetPath1);
            byte[] bytes2 = File.ReadAllBytes(targetPath2);

            bytes1.Should().Equal(bytes2);

            var dbpfParser = new DbpfPackageParser();
            var parse1 = await dbpfParser.ParseFileAsync(targetPath1);
            var parse2 = await dbpfParser.ParseFileAsync(targetPath2);

            parse1.Entries.Select(e => e.Id.FormattedKey).Should().Equal(parse2.Entries.Select(e => e.Id.FormattedKey));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteConversion_Ts4ToTs3_OnFailure_PreservesExistingValidTargetPackage()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_014_rollback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string existingValidTarget = Path.Combine(tempDir, "existing_valid_target.package");
            byte[] originalContent = Encoding.UTF8.GetBytes("PRESERVED_ORIGINAL_VALID_PACKAGE_PAYLOAD");
            File.WriteAllBytes(existingValidTarget, originalContent);

            string invalidSourcePath = Path.Combine(tempDir, "invalid_non_existent.package");

            var service = CreateService();
            var request = new DecorativeObjectConversionRequest(
                SourcePackagePath: invalidSourcePath,
                TargetOutputPath: existingValidTarget,
                TargetGameVersion: GameVersion.Sims3
            );

            var result = await service.ExecuteConversionAsync(request);

            result.IsSuccess.Should().BeFalse();
            File.Exists(existingValidTarget).Should().BeTrue();

            byte[] currentContent = File.ReadAllBytes(existingValidTarget);
            currentContent.Should().Equal(originalContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task RoundTrip_Ts3ToTs4ToTs3_PreservesResourceGraphIntegrity()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_014_roundtrip_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Step 1: Create initial TS3 source package
            string ts3InitialPath = CreateSyntheticTs3PackageFile(tempDir, "initial_ts3.package");
            string ts4IntermediatePath = Path.Combine(tempDir, "intermediate_ts4.package");
            string ts3FinalPath = Path.Combine(tempDir, "final_ts3.package");

            var service = CreateService();

            // Step 2: TS3 -> TS4 conversion
            var request1 = new DecorativeObjectConversionRequest(
                SourcePackagePath: ts3InitialPath,
                TargetOutputPath: ts4IntermediatePath,
                TargetGameVersion: GameVersion.Sims4
            );
            var result1 = await service.ExecuteConversionAsync(request1);
            result1.IsSuccess.Should().BeTrue();
            File.Exists(ts4IntermediatePath).Should().BeTrue();

            // Step 3: TS4 -> TS3 reverse conversion (Round-Trip)
            var request2 = new DecorativeObjectConversionRequest(
                SourcePackagePath: ts4IntermediatePath,
                TargetOutputPath: ts3FinalPath,
                TargetGameVersion: GameVersion.Sims3
            );
            var result2 = await service.ExecuteConversionAsync(request2);
            result2.IsSuccess.Should().BeTrue();
            File.Exists(ts3FinalPath).Should().BeTrue();

            // Verify final TS3 package is valid and contains TS3 resources
            var dbpfParser = new DbpfPackageParser();
            var parseFinal = await dbpfParser.ParseFileAsync(ts3FinalPath);

            parseFinal.IsSuccess.Should().BeTrue();
            parseFinal.Entries.Should().Contain(e => e.Id.TypeId == 0x015A1849); // GEOM
            parseFinal.Entries.Should().Contain(e => e.Id.TypeId == 0x01661233); // MODL
            parseFinal.Entries.Should().Contain(e => e.Id.TypeId == 0x01D10F34); // MLOD
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task SourceGameVersion_InferredAndDispatched_ForTs4ToTs3()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_014_r1_src_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string ts4SourcePath = CreateSyntheticTs4PackageFile(tempDir);
            string ts3TargetPath = Path.Combine(tempDir, "output_ts3.package");

            var request = new DecorativeObjectConversionRequest(
                SourcePackagePath: ts4SourcePath,
                TargetOutputPath: ts3TargetPath,
                TargetGameVersion: GameVersion.Sims3
            );

            request.EffectiveSourceGameVersion.Should().Be(GameVersion.Sims4);

            var dbpfParser = new DbpfPackageParser();
            var pkgInspector = new PackageInspectionService(dbpfParser);
            var validator = new CanonicalMeshValidator();
            var classifier = new MeshResourceClassifier();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var payloadReader = new PackageResourcePayloadReader();

            var meshInspector = new MeshInspectionService(pkgInspector, classifier, ts3Importer, ts4Importer, payloadReader);
            var texInspector = new TextureInspectionService(pkgInspector, new TextureResourceClassifier());
            var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(pkgInspector, meshInspector, texInspector);

            var packageResult = await pkgInspector.InspectFileAsync(ts4SourcePath);
            var sourceGraph = sourceGraphBuilder.BuildGraph(packageResult, request.EffectiveSourceGameVersion);

            sourceGraph.IsSourceGraphReady.Should().BeTrue();
            sourceGraph.MeshAssets.Should().NotBeEmpty();
            sourceGraph.MeshAssets[0].DetectedGameVersion.Should().Be(GameVersion.Sims4);
            sourceGraph.MeshAssets[0].FormatName.Should().Contain("TS4 Geometry");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void ResourceInspectorViewModel_DynamicConversionDirectionText_UpdatesWithTargetGameVersion()
    {
        var dbpfParser = new DbpfPackageParser();
        var pkgInspector = new PackageInspectionService(dbpfParser);
        var viewModel = new ResourceInspectorViewModel(pkgInspector);

        viewModel.TargetGameVersion = GameVersion.Sims4;
        viewModel.ConversionDirectionText.Should().Be("TS3 -> TS4");
        viewModel.ConvertButtonContent.Should().Be("Convert TS3 -> TS4");

        viewModel.TargetGameVersion = GameVersion.Sims3;
        viewModel.ConversionDirectionText.Should().Be("TS4 -> TS3");
        viewModel.ConvertButtonContent.Should().Be("Convert TS4 -> TS3");
    }
}
