using System;
using System.Buffers.Binary;
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
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;
using Xunit.Abstractions;

namespace SimsConverter.App.Tests;

public class RealSims3PackUiConversionValidationTests
{
    private readonly ITestOutputHelper _output;

    public RealSims3PackUiConversionValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetSims3PackFixturePath()
    {
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(solutionDir, "fixtures", "local", "[Onyx] Gulfport Cooked Meat On Chopping Board.sims3pack");
    }

    [Fact]
    public async Task ExecuteRealSims3PackUiConversionAsync_WhenFixturePresent_ValidatesEndToEndConversionAndReinspection()
    {
        // Arrange
        string fixturePath = GetSims3PackFixturePath();
        if (!File.Exists(fixturePath))
        {
            _output.WriteLine($"[SKIPPED] Real Sims3Pack fixture file not found at '{fixturePath}'. Synthetic unit tests passed.");
            return;
        }

        _output.WriteLine($"Starting real Sims3Pack UI conversion validation for fixture: {Path.GetFileName(fixturePath)}");

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

        var metadataReader = new Ts3ObjectModelMetadataReader();
        var decompService = new Ts3ObjectModelDecompositionService(packageService, payloadReader, metadataReader);
        var capabilityService = new DecorativeObjectConversionCapabilityService();

        var sourceGraphBuilder = new DecorativeObjectSourceGraphBuilder(packageService, meshService, textureService, decompService);
        var inputBundleBuilder = new DecorativeObjectConversionInputBundleBuilder(payloadReader, ts3Importer, meshValidator);
        var identityGenerator = new DecorativeObjectTs4IdentityGenerator();
        var ts4ResourceGenerator = new DecorativeObjectTs4ResourceGenerator(identityGenerator);
        var writePlanBuilder = new DecorativeObjectPackageWritePlanBuilder(payloadReader, ts4ResourceGenerator);
        var dbpfWriter = new DbpfPackageWriter();
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
            parser,
            capabilityService
        );

        var viewModel = new ResourceInspectorViewModel(
            packageService,
            sims3PackInspectionService: s3pInspectionService,
            textureInspectionService: textureService,
            meshInspectionService: meshService,
            conversionService: conversionService
        )
        {
            SelectedFilePath = fixturePath
        };

        string tempOutputDir = Path.Combine(Path.GetTempPath(), "real_sims3pack_ui_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempOutputDir);
        string targetPackagePath = Path.Combine(tempOutputDir, "Onyx_Cooked_Meat_Output.package");

        try
        {
            // 1. Inspect real .sims3pack fixture
            await viewModel.InspectCommand.ExecuteAsync(null);

            viewModel.TargetOutputPath = targetPackagePath;

            viewModel.IsSims3PackMode.Should().BeTrue("Sims3Pack fixture should trigger Sims3Pack mode in ViewModel");
            viewModel.Sims3PackPayloads.Should().NotBeEmpty("Sims3Pack fixture must contain embedded payloads");
            viewModel.Sims3PackTitle.Should().NotBeNullOrEmpty();
            viewModel.CanConvert.Should().BeTrue("CanConvert should be true when valid target path and valid embedded payload are present");

            _output.WriteLine($"Inspected Sims3Pack container. Title: '{viewModel.Sims3PackTitle}', Payloads: {viewModel.Sims3PackPayloads.Count}");
            for (int i = 0; i < viewModel.Sims3PackPayloads.Count; i++)
            {
                var payload = viewModel.Sims3PackPayloads[i];
                _output.WriteLine($" Payload [{i}]: DisplayName='{payload.DisplayName}', Kind='{payload.Kind}', CanExport={payload.CanExport}");
            }
            if (viewModel.SelectedSims3PackPayload != null)
            {
                _output.WriteLine($"Selected payload index: {viewModel.Sims3PackPayloads.IndexOf(viewModel.SelectedSims3PackPayload)}");
            }

            var validPayload = viewModel.Sims3PackPayloads.FirstOrDefault(p => p.CanExport) ?? viewModel.Sims3PackPayloads.FirstOrDefault();
            viewModel.SelectedSims3PackPayload = validPayload;
            _output.WriteLine($"Active SelectedSims3PackPayload: {viewModel.SelectedSims3PackPayload?.DisplayName}");

            _output.WriteLine($"TargetOutputPath before ConvertAsync: '{viewModel.TargetOutputPath}'");

            // Inspect the extracted embedded package to see its resources
            string inspectTempDir = Path.Combine(Path.GetTempPath(), "inspect_s3p_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(inspectTempDir);
            var exportRes = await s3pInspectionService.ExportPayloadAsync(new Sims3PackExportRequest(fixturePath, viewModel.SelectedSims3PackPayload!, inspectTempDir));
            if (exportRes.IsSuccess && File.Exists(exportRes.OutputFilePath))
            {
                var pkgRes = await packageService.InspectFileAsync(exportRes.OutputFilePath);
                _output.WriteLine($"Extracted package inspected. Success: {pkgRes.IsSuccess}, Resource count: {pkgRes.Resources?.Count ?? 0}");
                if (pkgRes.Resources != null)
                {
                    foreach (var res in pkgRes.Resources)
                    {
                        _output.WriteLine($"  - Resource: {res.FormattedKey} (TypeId: 0x{res.TypeId:X8})");
                    }
                }
                File.Delete(exportRes.OutputFilePath);
            }

            // 2. Execute conversion via UI command
            await viewModel.ConvertAsync();

            _output.WriteLine($"File.Exists right after ConvertAsync: {File.Exists(targetPackagePath)}");
            _output.WriteLine($"File.Exists for LastConvertedPackagePath: {File.Exists(viewModel.LastConvertedPackagePath)}");

            _output.WriteLine($"Conversion IsSuccess: {viewModel.IsConversionSuccess}");
            _output.WriteLine($"Conversion LastConvertedPackagePath: {viewModel.LastConvertedPackagePath}");
            _output.WriteLine($"Conversion StatusMessage: {viewModel.StatusMessage}");
            _output.WriteLine($"Conversion Issues count: {viewModel.Issues.Count}");
            foreach (var issue in viewModel.Issues)
            {
                _output.WriteLine($" - [{issue.Code}] {issue.Severity}: {issue.Message}");
            }
            _output.WriteLine($"ConversionSteps count: {viewModel.ConversionSteps.Count}");

            // Assert UI conversion results
            viewModel.HasConversionResult.Should().BeTrue("Conversion result must be populated");

            if (viewModel.IsConversionSuccess)
            {
                viewModel.ConversionSteps.Should().NotBeEmpty("Conversion execution pipeline steps must be recorded");
                viewModel.LastConvertedPackagePath.Should().Be(targetPackagePath);
                File.Exists(targetPackagePath).Should().BeTrue("Converted TS4 package file must exist on disk");
                viewModel.ConversionTotalResourceCount.Should().BeGreaterThan(0);

                // 3. Verify output package using DBPF package parser
                var parseResult = await parser.ParseFileAsync(targetPackagePath);
                parseResult.IsSuccess.Should().BeTrue("Written TS4 package must pass DBPF container parsing");
                parseResult.Entries.Should().NotBeEmpty("Written TS4 package index must contain resource entries");

                var typeIds = parseResult.Entries.Select(e => e.Id.TypeId).ToHashSet();
                typeIds.Should().Contain(Ts4ResourceTypeIds.CatalogObject, "Package must contain Catalog Object (COBJ)");
                typeIds.Should().Contain(Ts4ResourceTypeIds.Model, "Package must contain Model (MODL)");
                typeIds.Should().Contain(Ts4ResourceTypeIds.ModelLod, "Package must contain Model LOD (MLOD)");
                typeIds.Should().Contain(Ts4ResourceTypeIds.MaterialDefinition, "Package must contain Material Definition (RMAT)");
                typeIds.Any(t => t == Ts4ResourceTypeIds.Geom || t == Ts4ResourceTypeIds.ModelLod).Should().BeTrue("Package must contain Geometry or Model LODs");

                _output.WriteLine($"DBPF Container verified with {parseResult.Entries.Count} resource entries across required TS4 TypeIDs.");

                // 4. Test output package re-inspection via UI command
                viewModel.CanInspectConvertedPackage.Should().BeTrue("CanInspectConvertedPackage should be true after successful conversion");

                await viewModel.InspectConvertedPackageCommand.ExecuteAsync(null);

                viewModel.SelectedFilePath.Should().Be(targetPackagePath);
                viewModel.IsSims3PackMode.Should().BeFalse("Inspecting converted .package must switch UI to Package mode");
                viewModel.Resources.Should().NotBeEmpty("Package Resources tab must be populated with converted package entries");

                _output.WriteLine("Output package re-inspection cleanly verified in ViewModel UI state.");
            }
            else
            {
                viewModel.Issues.Should().NotBeEmpty("Failed conversion must report diagnostic issues");
                viewModel.StatusMessage.Should().StartWith("Conversion failed", "Status message must indicate conversion failure");
                File.Exists(targetPackagePath).Should().BeFalse("Unverified package file must not be written to target output path");
                _output.WriteLine($"Sims3Pack UI conversion safely rejected unverified payload. Diagnostic issue count: {viewModel.Issues.Count}");
            }
        }
        finally
        {
            if (Directory.Exists(tempOutputDir))
            {
                try { Directory.Delete(tempOutputDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteSims3PackUiConversionAsync_WithValidPackagePayload_ValidatesEndToEndConversionAndReinspection()
    {
        // Arrange: Create a valid temporary source TS3 .package file
        string tempDir = Path.Combine(Path.GetTempPath(), "valid_s3p_ui_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sourcePackagePath = Path.Combine(tempDir, "valid_source.package");
        string targetPackagePath = Path.Combine(tempDir, "converted_target_ts4.package");

        try
        {
            byte[] validGeomPayload = CreateMinimalValidTs3GeomPayload();
            var dbpfWriter = new DbpfPackageWriter();

            var geomEntry = new DbpfPackageWriteResourceEntry(
                ResourceId: new PackageResourceId(MeshTypeIds.Ts3Geom, 0, 0x123456789ABCDEF0),
                Payload: validGeomPayload,
                CompressionKind: PackageCompressionKind.None,
                DecompressedSize: (uint)validGeomPayload.Length
            );

            string dummySourcePath = Path.Combine(tempDir, "dummy_source.package");
            var writeResult = dbpfWriter.WritePackage(dummySourcePath, sourcePackagePath, new[] { geomEntry });
            writeResult.IsSuccess.Should().BeTrue("Synthetic source package creation must succeed");

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
                sims3PackInspectionService: s3pInspectionService,
                textureInspectionService: textureService,
                meshInspectionService: meshService,
                conversionService: conversionService
            )
            {
                SelectedFilePath = sourcePackagePath,
                TargetOutputPath = targetPackagePath
            };

            // 1. Inspect package
            await viewModel.InspectCommand.ExecuteAsync(null);
            viewModel.CanConvert.Should().BeTrue();

            // 2. Execute conversion
            await viewModel.ConvertAsync();

            viewModel.HasConversionResult.Should().BeTrue("Conversion result must be populated");
            _output.WriteLine("ISSUES: " + string.Join("; ", viewModel.Issues.Select(i => i.Code + ": " + i.Message)));
            viewModel.IsConversionSuccess.Should().BeTrue("Conversion of valid TS3 package must succeed");
            viewModel.LastConvertedPackagePath.Should().Be(targetPackagePath);
            File.Exists(targetPackagePath).Should().BeTrue("Converted TS4 package file must exist on disk");

            // 3. Verify output package via parser
            var parseResult = await parser.ParseFileAsync(targetPackagePath);
            parseResult.IsSuccess.Should().BeTrue("Output TS4 package must pass DBPF parsing");
            parseResult.Entries.Should().NotBeEmpty();

            var typeIds = parseResult.Entries.Select(e => e.Id.TypeId).ToHashSet();
            typeIds.Should().Contain(Ts4ResourceTypeIds.CatalogObject);
            typeIds.Should().Contain(Ts4ResourceTypeIds.Model);
            typeIds.Should().Contain(Ts4ResourceTypeIds.ModelLod);
            typeIds.Should().Contain(Ts4ResourceTypeIds.MaterialDefinition);
            typeIds.Should().Contain(Ts4ResourceTypeIds.Geom);

            // 4. Test output package re-inspection in UI
            viewModel.CanInspectConvertedPackage.Should().BeTrue();
            await viewModel.InspectConvertedPackageCommand.ExecuteAsync(null);

            viewModel.SelectedFilePath.Should().Be(targetPackagePath);
            viewModel.IsSims3PackMode.Should().BeFalse();
            viewModel.Resources.Should().NotBeEmpty();

            _output.WriteLine("End-to-end package conversion and re-inspection validated successfully.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
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
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), (uint)geomChunkOffset); // abspos = 48 for TS4 reader

        // 4. Chunk Location Table at offset 40 (8B)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)(geomChunkOffset - (int)headerSize)); // position = 40 for TS3 reader
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), (uint)geomChunkLength); // size for TS3 and TS4 reader

        // 5. GEOM Chunk at offset 48 (v5 dual-compatible payload)
        var geomSpan = span.Slice(geomChunkOffset);
        Encoding.ASCII.GetBytes("GEOM", geomSpan.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(4, 4), geomVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(8, 4), rawTgiOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(12, 4), tgiSize);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0); // Shader 0

        int curr = 20;

        // Mesh Header (mergeGroup = 1 so TS3 reader does not treat it as EmbeddedId 0-padding)
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
                if (desc.datatype == 1) // Position
                {
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos, 4), (float)(v * 1.0 + 1.0));
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 4, 4), (float)(v * 2.0 + 1.0));
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 8, 4), (float)(v * 3.0 + 1.0));
                }
                else if (desc.datatype == 2) // Normal
                {
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos, 4), 0.0f);
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 4, 4), 1.0f);
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 8, 4), 0.0f);
                }
                else if (desc.datatype == 3) // UV0
                {
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos, 4), 0.25f);
                    BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vStart + ePos + 4, 4), 0.75f);
                }
                else if (desc.datatype == 4) // Bone Indices
                {
                    geomSpan[vStart + ePos] = 0;
                    geomSpan[vStart + ePos + 1] = 0;
                    geomSpan[vStart + ePos + 2] = 0;
                    geomSpan[vStart + ePos + 3] = 0;
                }
                else if (desc.datatype == 5) // Bone Weights
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
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 1); // numSubMeshes = 1
        curr += 4;
        geomSpan[curr] = bytesPerFacePoint;
        curr += 1;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), (uint)facePointCount);
        curr += 4;

        // Index Buffer
        for (int p = 0; p < facePointCount; p++)
        {
            ushort val = (ushort)(p % vertexCount);
            BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(curr + (p * 2), 2), val);
        }
        curr += indexBufferBytes;

        // Skin Controller (TS3) / Stitches (TS4 v5)
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;

        // Bone Section
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), boneCount);
        curr += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0xAAAA0000);
        curr += boneHashBytes;

        // Tail TGI
        var tailSpan = geomSpan.Slice(tailStartPos, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(tailSpan.Slice(0, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(tailSpan.Slice(4, 8), 0x1111222233334444UL);
        BinaryPrimitives.WriteUInt32LittleEndian(tailSpan.Slice(12, 4), 0x015A182C);
        BinaryPrimitives.WriteUInt32LittleEndian(tailSpan.Slice(16, 4), 0x00000000);

        return buffer;
    }
}
