using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class DeterministicTs4OutputArtifactValidationTests
{
    private readonly ITestOutputHelper _output;

    public DeterministicTs4OutputArtifactValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetSims3PackFixturePath()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(solutionDir, "fixtures", "local", "[Onyx] Gulfport Cooked Meat On Chopping Board.sims3pack");
    }

    private static byte[] CreateMinimalValidTs3GeomPayload()
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
        int skinControllerOrStitchesBytes = 4; // 4B 0 for SkinController (TS3) / Stitches (TS4 v5)
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
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1); // ExternalTgiCount = 1
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1); // InternalResourceCount = 1

        // 2. Internal ITG at offset 8 (16B)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), 0x5555666677778888UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), 0x015A1849); // GEOM TypeId
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0x00000000);

        // 3. External TGI at offset 24 (16B)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0x015A182C); // RMAT Material TypeId
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
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0); // Shader 0

        int curr = 20;

        // Mesh Header
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 8, 4), (uint)vertexCount);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), (uint)elementCount);
        curr += 16;

        // Element Descriptors
        foreach (var desc in descriptors)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), desc.datatype);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), desc.format);
            geomSpan[curr + 8] = desc.size;
            curr += 9;
        }

        // Vertex Buffer
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

    private static (IDecorativeObjectConversionService conversionService, ResourceInspectorViewModel viewModel, string sourcePackagePath) CreateTestEnvironment(string tempDir)
    {
        string sourcePackagePath = Path.Combine(tempDir, "valid_source.package");
        byte[] validGeomPayload = CreateMinimalValidTs3GeomPayload();
        var dbpfWriter = new DbpfPackageWriter();

        var geomEntry = new DbpfPackageWriteResourceEntry(
            ResourceId: new PackageResourceId(MeshTypeIds.Ts3Geom, 0, 0x123456789ABCDEF0),
            Payload: validGeomPayload,
            CompressionKind: PackageCompressionKind.None,
            DecompressedSize: (uint)validGeomPayload.Length
        );

        string dummySourcePath = Path.Combine(tempDir, "dummy_source.package");
        dbpfWriter.WritePackage(dummySourcePath, sourcePackagePath, new[] { geomEntry });

        IDbpfPackageParser parser = new DbpfPackageParser();
        IPackageInspectionService packageService = new PackageInspectionService(parser);
        ISims3PackDetector detector = new Sims3PackDetector();
        ISims3PackXmlParser xmlParser = new Sims3PackXmlParser();
        ISims3PackPayloadCatalogScanner catalogScanner = new Sims3PackPayloadCatalogScanner();
        ISims3PackPayloadExporter exporter = new Sims3PackPayloadExporter();
        ISims3PackInspectionService s3pInspectionService = new Sims3PackInspectionService(detector, xmlParser, catalogScanner, exporter);

        var meshClassifier = new MeshResourceClassifier();
        var ts3Importer = new Ts3GeomCanonicalMeshImporter();
        var ts4MetadataReader = new Ts4GeomMetadataReader();
        var meshValidator = new CanonicalMeshValidator();
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(ts4MetadataReader, meshValidator);
        var payloadReader = new PackageResourcePayloadReader();
        var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);

        var textureClassifier = new TextureResourceClassifier();
        var textureService = new TextureInspectionService(packageService, textureClassifier);

        var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService);
        var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, meshValidator);
        var identityGenerator = new DecorativeObjectTs4IdentityGenerator();
        var ts4ResourceGenerator = new DecorativeObjectTs4ResourceGenerator(identityGenerator);
        var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader, ts4ResourceGenerator);
        var packageWriter = new DecorativeObjectPackageWriter(dbpfWriter);
        var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

        IDecorativeObjectConversionService conversionService = new DecorativeObjectConversionService(
            packageService,
            meshService,
            textureService,
            sourceGraphBuilder,
            inputBundleBuilder,
            writePlanBuilder,
            packageWriter,
            payloadVerifier,
            parser
        );

        var viewModel = new ResourceInspectorViewModel(
            packageService,
            exportService: null,
            filePickerService: null,
            sims3PackInspectionService: s3pInspectionService,
            textureInspectionService: textureService,
            meshInspectionService: meshService,
            conversionService: conversionService
        )
        {
            SelectedFilePath = sourcePackagePath
        };

        return (conversionService, viewModel, sourcePackagePath);
    }

    private class FailingPayloadVerifier : ITs4ResourcePayloadCompatibilityVerifier
    {
        public Task<Ts4PayloadCompatibilityResult> VerifyPackagePayloadsAsync(string packagePath, DbpfParseResult parseResult, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VerifyPackagePayloads(packagePath, parseResult));
        }

        public Ts4PayloadCompatibilityResult VerifyPackagePayloads(string packagePath, DbpfParseResult parseResult)
        {
            return new Ts4PayloadCompatibilityResult(
                IsSuccess: false,
                TotalResourcesVerified: 0,
                VerifiedTgiLinkCount: 0,
                Issues: new[] { new ConversionIssue("FAIL001", "Simulated payload compatibility verification failure.", ConversionIssueSeverity.Error) }
            );
        }
    }

    [Fact]
    public async Task ExecuteConsecutiveConversions_WithValidPackage_ProducesIdenticalByteForByteOutputAndDeterministicGraph()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "det_conv_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string targetPath1 = Path.Combine(tempDir, "converted_ts4_run1.package");
        string targetPath2 = Path.Combine(tempDir, "converted_ts4_run2.package");

        try
        {
            var env1 = CreateTestEnvironment(tempDir);
            var env2 = CreateTestEnvironment(tempDir);

            // Run 1 via ViewModel
            await env1.viewModel.InspectCommand.ExecuteAsync(null);
            env1.viewModel.TargetOutputPath = targetPath1;
            await env1.viewModel.ConvertAsync();

            _output.WriteLine($"Run 1 StatusMessage: {env1.viewModel.StatusMessage}");
            env1.viewModel.IsConversionSuccess.Should().BeTrue("Run 1 conversion must succeed");
            File.Exists(targetPath1).Should().BeTrue("Run 1 output file must exist");

            // Run 2 via ViewModel
            await env2.viewModel.InspectCommand.ExecuteAsync(null);
            env2.viewModel.TargetOutputPath = targetPath2;
            await env2.viewModel.ConvertAsync();

            _output.WriteLine($"Run 2 StatusMessage: {env2.viewModel.StatusMessage}");
            env2.viewModel.IsConversionSuccess.Should().BeTrue("Run 2 conversion must succeed");
            File.Exists(targetPath2).Should().BeTrue("Run 2 output file must exist");

            // 1. Compare UI state
            env1.viewModel.ConversionTotalResourceCount.Should().Be(env2.viewModel.ConversionTotalResourceCount, "Resource counts must be deterministic");
            env1.viewModel.ConversionVerifiedLinkCount.Should().Be(env2.viewModel.ConversionVerifiedLinkCount, "Verified link counts must be deterministic");

            // 2. Byte-for-Byte comparison
            byte[] run1Bytes = await File.ReadAllBytesAsync(targetPath1);
            byte[] run2Bytes = await File.ReadAllBytesAsync(targetPath2);

            run1Bytes.Should().Equal(run2Bytes, "Consecutive conversion outputs must be 100% byte-for-byte identical");

            // 3. DBPF Index & Resource Graph level comparison
            IDbpfPackageParser parser = new DbpfPackageParser();
            IPackageResourcePayloadReader payloadReader = new PackageResourcePayloadReader();

            var parseResult1 = await parser.ParseFileAsync(targetPath1);
            var parseResult2 = await parser.ParseFileAsync(targetPath2);

            parseResult1.IsSuccess.Should().BeTrue();
            parseResult2.IsSuccess.Should().BeTrue();
            parseResult1.Entries.Count.Should().Be(parseResult2.Entries.Count, "Package index entry counts must match");

            for (int i = 0; i < parseResult1.Entries.Count; i++)
            {
                var entry1 = parseResult1.Entries[i];
                var entry2 = parseResult2.Entries[i];

                entry1.Id.FormattedKey.Should().Be(entry2.Id.FormattedKey, $"Entry {i} Resource ID must match");
                entry1.CompressedSize.Should().Be(entry2.CompressedSize, $"Entry {i} CompressedSize must match");
                entry1.DecompressedSize.Should().Be(entry2.DecompressedSize, $"Entry {i} DecompressedSize must match");

                var payload1 = payloadReader.ReadPayload(targetPath1, entry1);
                var payload2 = payloadReader.ReadPayload(targetPath2, entry2);
                payload1.IsSuccess.Should().BeTrue();
                payload2.IsSuccess.Should().BeTrue();
                payload1.Payload!.ToArray().Should().Equal(payload2.Payload!.ToArray(), $"Entry {i} payload must match byte-for-byte");
            }

            _output.WriteLine($"Deterministic TS4 artifact validation clean pass: {run1Bytes.Length} bytes, {parseResult1.Entries.Count} resource entries matched byte-for-byte.");
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
    public async Task ExecuteConversion_WhenTargetOutputFileExists_AtomicallyOverwritesTargetPackageWithValidOutput()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "overwrite_conv_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string targetPath = Path.Combine(tempDir, "existing_target_ts4.package");

        try
        {
            // Seed target path with a dummy existing file
            await File.WriteAllTextAsync(targetPath, "DUMMY PRE-EXISTING PACKAGE CONTENT THAT MUST BE OVERWRITTEN");
            File.Exists(targetPath).Should().BeTrue("Target file must exist prior to overwrite test");

            var env = CreateTestEnvironment(tempDir);
            await env.viewModel.InspectCommand.ExecuteAsync(null);
            env.viewModel.TargetOutputPath = targetPath;
            await env.viewModel.ConvertAsync();

            env.viewModel.IsConversionSuccess.Should().BeTrue("Conversion overwriting existing target file must succeed");
            File.Exists(targetPath).Should().BeTrue("Target output package file must exist");

            IDbpfPackageParser parser = new DbpfPackageParser();
            var parseResult = await parser.ParseFileAsync(targetPath);
            parseResult.IsSuccess.Should().BeTrue("Overwritten target package must pass DBPF container parsing");
            parseResult.Entries.Should().NotBeEmpty("Overwritten target package index must contain resource entries");

            _output.WriteLine($"Atomic overwrite clean pass. Target output package successfully replaced with {parseResult.Entries.Count} resources.");
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
    public async Task ExecuteConversion_WhenConversionFailsAndTargetFileAlreadyExisted_RestoresOriginalTargetPackageFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "restore_guard_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string targetPath = Path.Combine(tempDir, "pre_existing_valid_target.package");
        const string originalContent = "ORIGINAL PRE-EXISTING VALID PACKAGE CONTENT THAT MUST BE PRESERVED ON FAILURE";

        try
        {
            // Seed pre-existing valid content at target output path
            await File.WriteAllTextAsync(targetPath, originalContent);
            File.Exists(targetPath).Should().BeTrue("Pre-existing target output file must exist");

            var env = CreateTestEnvironment(tempDir);

            // Execute conversion with invalid source path to trigger execution failure
            string invalidSourcePath = Path.Combine(tempDir, "non_existent_source.package");
            var invalidRequest = new DecorativeObjectConversionRequest(
                invalidSourcePath,
                targetPath,
                GameVersion.Sims4
            );

            var failedResult = await env.conversionService.ExecuteConversionAsync(invalidRequest);

            failedResult.IsSuccess.Should().BeFalse("Conversion with invalid source must fail");
            File.Exists(targetPath).Should().BeTrue("Pre-existing valid target file MUST be preserved on conversion failure");

            string currentContent = await File.ReadAllTextAsync(targetPath);
            currentContent.Should().Be(originalContent, "Pre-existing target file contents MUST match original valid content after failure rollback");

            _output.WriteLine("Target preservation clean pass. Pre-existing valid target output file was cleanly preserved on conversion failure.");
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
    public async Task ExecuteConversion_WhenPostWriteValidationFailsAndTargetFileExisted_PreservesPreExistingTargetFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "post_write_restore_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string targetPath = Path.Combine(tempDir, "pre_existing_target.package");
        const string originalContent = "PRE-EXISTING VALID OUTPUT PACKAGE BEFORE POST-WRITE FAILURE";

        try
        {
            await File.WriteAllTextAsync(targetPath, originalContent);
            File.Exists(targetPath).Should().BeTrue("Pre-existing target output file must exist");

            IDbpfPackageParser parser = new DbpfPackageParser();
            IPackageInspectionService packageService = new PackageInspectionService(parser);
            IMeshInspectionService meshService = new MeshInspectionService(packageService, new MeshResourceClassifier(), new Ts3GeomCanonicalMeshImporter(), new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), new CanonicalMeshValidator()), new PackageResourcePayloadReader());
            ITextureInspectionService textureService = new TextureInspectionService(packageService, new TextureResourceClassifier());
            IDecorativeObjectSourceGraphBuilder sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService);
            IDecorativeObjectConversionInputBundleBuilder inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(new PackageResourcePayloadReader(), new Ts3GeomCanonicalMeshImporter(), new CanonicalMeshValidator());
            IDecorativeObjectTs4IdentityGenerator identityGenerator = new DecorativeObjectTs4IdentityGenerator();
            IDecorativeObjectPackageWritePlanBuilder writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(new PackageResourcePayloadReader(), new DecorativeObjectTs4ResourceGenerator(identityGenerator));
            IDecorativeObjectPackageWriter packageWriter = new DecorativeObjectPackageWriter(new DbpfPackageWriter());

            // Use FailingPayloadVerifier to simulate post-write payload compatibility verification failure
            ITs4ResourcePayloadCompatibilityVerifier failingVerifier = new FailingPayloadVerifier();

            IDecorativeObjectConversionService failingConversionService = new DecorativeObjectConversionService(
                packageService,
                meshService,
                textureService,
                sourceGraphBuilder,
                inputBundleBuilder,
                writePlanBuilder,
                packageWriter,
                failingVerifier,
                parser
            );

            string validSourcePath = Path.Combine(tempDir, "valid_source.package");
            byte[] validGeomPayload = CreateMinimalValidTs3GeomPayload();
            var dbpfWriter = new DbpfPackageWriter();
            dbpfWriter.WritePackage(Path.Combine(tempDir, "dummy.package"), validSourcePath, new[] {
                new DbpfPackageWriteResourceEntry(new PackageResourceId(MeshTypeIds.Ts3Geom, 0, 0x123456789ABCDEF0), validGeomPayload, PackageCompressionKind.None, (uint)validGeomPayload.Length)
            });

            var request = new DecorativeObjectConversionRequest(validSourcePath, targetPath, GameVersion.Sims4);
            var result = await failingConversionService.ExecuteConversionAsync(request);

            result.IsSuccess.Should().BeFalse("Conversion with failing verifier must fail");
            File.Exists(targetPath).Should().BeTrue("Pre-existing target file MUST be preserved on post-write validation failure");

            string currentContent = await File.ReadAllTextAsync(targetPath);
            currentContent.Should().Be(originalContent, "Pre-existing target file content MUST be untouched when post-write validation fails");

            _output.WriteLine("Post-write validation failure preservation clean pass. Pre-existing target output remained untouched.");
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
    public async Task ExecuteConversion_WhenConversionFailsAndTargetFileDidNotExist_DeletesNewlyWrittenArtifact()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "clean_guard_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string targetPath = Path.Combine(tempDir, "non_existent_target.package");

        try
        {
            File.Exists(targetPath).Should().BeFalse("Target path must not exist prior to test");

            var env = CreateTestEnvironment(tempDir);

            string invalidSourcePath = Path.Combine(tempDir, "non_existent_source.package");
            var invalidRequest = new DecorativeObjectConversionRequest(
                invalidSourcePath,
                targetPath,
                GameVersion.Sims4
            );

            var failedResult = await env.conversionService.ExecuteConversionAsync(invalidRequest);

            failedResult.IsSuccess.Should().BeFalse("Conversion with invalid source must fail");
            File.Exists(targetPath).Should().BeFalse("No output file must remain at target path when conversion fails and target did not exist");

            _output.WriteLine("Artifact cleanup clean pass. Target output file was cleanly removed on failed conversion execution.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
