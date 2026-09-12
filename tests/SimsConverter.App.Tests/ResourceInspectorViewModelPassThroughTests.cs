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
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;

namespace SimsConverter.App.Tests;

public class ResourceInspectorViewModelPassThroughTests
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
    public async Task ResourceInspectorViewModel_PreflightCapabilityCounts_MatchOutputResourceSetReport()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_ui_passthrough_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sourcePackagePath = Path.Combine(tempDir, "source_ui_mixed.package");

        try
        {
            var geomId = new PackageResourceId(0x015A1849, 0, 0x10);
            var stblId = new PackageResourceId(0x220557DA, 0, 0x20); // PassThrough
            var itunId = new PackageResourceId(0x03B33DDF, 0, 0x30); // Known Unsupported (ITUN)

            byte[] geomPayload = CreateMinimalValidGeomPayload();
            byte[] stblPayload = new byte[] { 0x53, 0x54, 0x42, 0x4C, 0xAA, 0xBB };
            byte[] itunPayload = new byte[] { 0x49, 0x54, 0x55, 0x4E, 0x11, 0x22 };

            CreateTestDbpfPackage(sourcePackagePath, new[]
            {
                (geomId, geomPayload),
                (stblId, stblPayload),
                (itunId, itunPayload)
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

            var viewModel = new ResourceInspectorViewModel(
                packageService,
                textureInspectionService: textureService,
                meshInspectionService: meshService,
                conversionService: conversionService,
                capabilityService: capabilityService
            );

            viewModel.SelectedFilePath = sourcePackagePath;
            await viewModel.InspectCommand.ExecuteAsync(null);

            viewModel.HasCapabilityMatrix.Should().BeTrue();
            viewModel.CapabilitySupportedCount.Should().Be(1);
            viewModel.CapabilityPassThroughCount.Should().Be(1);
            viewModel.CapabilityUnsupportedCount.Should().Be(1);

            // Execute conversion via UI command
            viewModel.CanConvert.Should().BeTrue();
            await viewModel.ConvertCommand.ExecuteAsync(null);

            viewModel.IsConversionSuccess.Should().BeTrue();

            int preservedSourceCount = viewModel.CapabilitySupportedCount + viewModel.CapabilityPassThroughCount;
            preservedSourceCount.Should().Be(2);

            viewModel.ConversionTotalResourceCount.Should().Be(6);
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
