using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;

namespace SimsConverter.Application.Tests;

public class UnsupportedResourceExclusionAndPassThroughTests
{
    private static void CreateTestDbpfPackage(string targetPackagePath, IEnumerable<(PackageResourceId Id, byte[] Payload)> resources)
    {
        string tempDir = Path.GetDirectoryName(targetPackagePath) ?? Path.GetTempPath();
        string dummySourcePath = Path.Combine(tempDir, "dummy_" + Guid.NewGuid().ToString("N") + ".package");

        var dbpfWriter = new DbpfPackageWriter();
        var entries = resources.Select(r => new DbpfPackageWriteResourceEntry(
            ResourceId: r.Id,
            Payload: r.Payload,
            CompressionKind: PackageCompressionKind.None,
            DecompressedSize: (uint)r.Payload.Length
        )).ToList();

        var writeResult = dbpfWriter.WritePackage(dummySourcePath, targetPackagePath, entries);
        writeResult.IsSuccess.Should().BeTrue();
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
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x015A1849);
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

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        geomSpan[curr + 4] = bytesPerFacePoint;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 5, 4), (uint)facePointCount);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 9, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 11, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + 13, 2), 2);
        curr += submeshBytes;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)boneCount);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 0x12345678);
        curr += boneSectionBytes;

        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(geomSpan.Slice(curr + 4, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), 0x015A182C);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 16, 4), 0x00000000);

        return buffer;
    }

    [Fact]
    public async Task ExecuteTs3ToTs4Conversion_ExcludesUnsupportedResources_AndPreservesPassThroughByteIntegrity()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_passthrough_ts3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sourcePackagePath = Path.Combine(tempDir, "source_ts3_mixed.package");
        string targetOutputPath = Path.Combine(tempDir, "target_ts4_output.package");

        try
        {
            var geomId = new PackageResourceId(0x015A1849, 0, 0x100);
            var stblId = new PackageResourceId(0x220557DA, 0, 0x200); // STBL (PassThrough)
            var itunId = new PackageResourceId(0x03B33DDF, 0, 0x300); // ITUN (Known Unsupported)
            var unknownId = new PackageResourceId(0x99999999, 0, 0x400); // Unknown Unsupported (CAPA002)

            byte[] geomPayload = CreateMinimalValidGeomPayload();
            byte[] stblPayload = new byte[] { 0x53, 0x54, 0x42, 0x4C, 0x01, 0x02, 0x03, 0x04, 0xAA, 0xBB, 0xCC, 0xDD };
            byte[] itunPayload = new byte[] { 0x49, 0x54, 0x55, 0x4E, 0x99, 0x88 };
            byte[] unknownPayload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            CreateTestDbpfPackage(sourcePackagePath, new[]
            {
                (geomId, geomPayload),
                (stblId, stblPayload),
                (itunId, itunPayload),
                (unknownId, unknownPayload)
            });

            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var meshValidator = new CanonicalMeshValidator();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), meshValidator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), meshValidator);

            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);

            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);

            var capabilityService = new DecorativeObjectConversionCapabilityService();
            var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService);
            var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, meshValidator);
            var ts4ResourceGenerator = new DecorativeObjectTs4ResourceGenerator();
            var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader, ts4ResourceGenerator);
            var dbpfWriter = new DbpfPackageWriter();
            var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);

            var conversionService = new DecorativeObjectConversionService(
                packageInspectionService: packageService,
                meshInspectionService: meshService,
                textureInspectionService: textureService,
                sourceGraphBuilder: sourceGraphBuilder,
                inputBundleBuilder: inputBundleBuilder,
                writePlanBuilder: writePlanBuilder,
                packageWriter: packageWriter
            );

            // Preflight capability check
            var pkgResult = await packageService.InspectFileAsync(sourcePackagePath);
            pkgResult.IsSuccess.Should().BeTrue();
            var capabilityMatrix = capabilityService.EvaluateCapability(pkgResult, GameVersion.Sims3, GameVersion.Sims4);

            capabilityMatrix.SupportedResourceCount.Should().Be(1, "1 GEOM mesh resource");
            capabilityMatrix.PassThroughResourceCount.Should().Be(1, "1 STBL pass-through resource");
            capabilityMatrix.UnsupportedResourceCount.Should().Be(2, "1 ITUN + 1 Unknown resource");
            capabilityMatrix.Issues.Should().Contain(i => i.Code == "CAPA001", "ITUN emits CAPA001 warning");
            capabilityMatrix.Issues.Should().Contain(i => i.Code == "CAPA002", "Unknown TypeId 0x99999999 emits CAPA002 warning");

            // Execute conversion TS3 -> TS4
            var request = new DecorativeObjectConversionRequest(SourcePackagePath: sourcePackagePath, TargetOutputPath: targetOutputPath, TargetGameVersion: GameVersion.Sims4);
            var conversionResult = await conversionService.ExecuteConversionAsync(request);

            conversionResult.IsSuccess.Should().BeTrue("Conversion must succeed for valid TS3 object input.");
            File.Exists(targetOutputPath).Should().BeTrue();

            var outputPkgResult = await packageService.InspectFileAsync(targetOutputPath);
            outputPkgResult.IsSuccess.Should().BeTrue();

            // 1. Verify unsupported resources ITUN and 0x99999999 are strictly EXCLUDED
            outputPkgResult.Resources.Should().NotContain(r => r.TypeId == itunId.TypeId, "ITUN (0x03B33DDF) must be excluded from target output package.");
            outputPkgResult.Resources.Should().NotContain(r => r.TypeId == unknownId.TypeId, "Unknown resource 0x99999999 must be excluded from target output package.");

            // 2. Verify STBL pass-through resource IS PRESENT and has exact byte payload integrity
            outputPkgResult.Resources.Should().Contain(r => r.TypeId == stblId.TypeId, "STBL resource must be written to target output package.");
            var outputStblRow = outputPkgResult.Resources.First(r => r.TypeId == stblId.TypeId);

            var stblReadResult = payloadReader.ReadPayload(targetOutputPath, outputStblRow.ToEntry());
            stblReadResult.IsSuccess.Should().BeTrue();
            stblReadResult.Payload.Should().Equal(stblPayload, "Pass-through STBL payload must preserve 100% byte integrity.");
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
    public async Task ExecuteTs4ToTs3Conversion_ExcludesUnsupportedResources_AndPreservesPassThroughByteIntegrity()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_passthrough_ts4_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sourcePackagePath = Path.Combine(tempDir, "source_ts4_mixed.package");
        string targetOutputPath = Path.Combine(tempDir, "target_ts3_output.package");

        try
        {
            var geomId = new PackageResourceId(0x015A1849, 0, 0x1000);
            var thumId = new PackageResourceId(0x0D64DFF0, 0, 0x2000); // THUM (PassThrough)
            var scriptId = new PackageResourceId(0x2800D61B, 0, 0x3000); // S4SCRIPT (Known Unsupported)
            var unknownId = new PackageResourceId(0x77777777, 0, 0x4000); // Unknown Unsupported (CAPA002)

            byte[] geomPayload = CreateMinimalValidGeomPayload();
            byte[] thumPayload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04 };
            byte[] scriptPayload = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };
            byte[] unknownPayload = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };

            CreateTestDbpfPackage(sourcePackagePath, new[]
            {
                (geomId, geomPayload),
                (thumId, thumPayload),
                (scriptId, scriptPayload),
                (unknownId, unknownPayload)
            });

            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var payloadReader = new PackageResourcePayloadReader();
            var meshValidator = new CanonicalMeshValidator();
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), meshValidator);
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), meshValidator);

            var meshClassifier = new MeshResourceClassifier();
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);

            var textureClassifier = new TextureResourceClassifier();
            var textureService = new TextureInspectionService(packageService, textureClassifier);

            var capabilityService = new DecorativeObjectConversionCapabilityService();
            var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService);
            var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, meshValidator, ts4Importer);
            var ts3ResourceGenerator = new DecorativeObjectTs3ResourceGenerator();
            var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader, ts3ResourceGenerator: ts3ResourceGenerator);
            var dbpfWriter = new DbpfPackageWriter();
            var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);

            var conversionService = new DecorativeObjectConversionService(
                packageInspectionService: packageService,
                meshInspectionService: meshService,
                textureInspectionService: textureService,
                sourceGraphBuilder: sourceGraphBuilder,
                inputBundleBuilder: inputBundleBuilder,
                writePlanBuilder: writePlanBuilder,
                packageWriter: packageWriter
            );

            // Preflight capability check TS4 -> TS3
            var pkgResult = await packageService.InspectFileAsync(sourcePackagePath);
            pkgResult.IsSuccess.Should().BeTrue();
            var capabilityMatrix = capabilityService.EvaluateCapability(pkgResult, GameVersion.Sims4, GameVersion.Sims3);

            capabilityMatrix.SupportedResourceCount.Should().Be(1, "1 GEOM mesh resource");
            capabilityMatrix.PassThroughResourceCount.Should().Be(1, "1 THUM pass-through resource");
            capabilityMatrix.UnsupportedResourceCount.Should().Be(2, "1 S4SCRIPT + 1 Unknown resource");
            capabilityMatrix.Issues.Should().Contain(i => i.Code == "CAPA001", "S4SCRIPT emits CAPA001 warning");
            capabilityMatrix.Issues.Should().Contain(i => i.Code == "CAPA002", "Unknown TypeId 0x77777777 emits CAPA002 warning");

            // Execute reverse conversion TS4 -> TS3
            var request = new DecorativeObjectConversionRequest(SourcePackagePath: sourcePackagePath, TargetOutputPath: targetOutputPath, TargetGameVersion: GameVersion.Sims3);
            var conversionResult = await conversionService.ExecuteConversionAsync(request);

            conversionResult.IsSuccess.Should().BeTrue("Conversion must succeed for valid TS4 object input.");
            File.Exists(targetOutputPath).Should().BeTrue();

            var outputPkgResult = await packageService.InspectFileAsync(targetOutputPath);
            outputPkgResult.IsSuccess.Should().BeTrue();

            // 1. Verify unsupported resources S4SCRIPT and 0x77777777 are strictly EXCLUDED
            outputPkgResult.Resources.Should().NotContain(r => r.TypeId == scriptId.TypeId, "S4SCRIPT (0x2800D61B) must be excluded from target TS3 output package.");
            outputPkgResult.Resources.Should().NotContain(r => r.TypeId == unknownId.TypeId, "Unknown resource 0x77777777 must be excluded from target TS3 output package.");

            // 2. Verify THUM pass-through resource IS PRESENT and has exact byte payload integrity
            outputPkgResult.Resources.Should().Contain(r => r.TypeId == thumId.TypeId, "THUM resource must be written to target TS3 output package.");
            var outputThumRow = outputPkgResult.Resources.First(r => r.TypeId == thumId.TypeId);

            var thumReadResult = payloadReader.ReadPayload(targetOutputPath, outputThumRow.ToEntry());
            thumReadResult.IsSuccess.Should().BeTrue();
            thumReadResult.Payload.Should().Equal(thumPayload, "Pass-through THUM payload must preserve 100% byte integrity.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
