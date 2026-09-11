using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.Services;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using Xunit;

namespace SimsConverter.App.Tests;

public class ResourceInspectorViewModelConversionTests
{
    [Fact]
    public async Task ConvertAsync_GivenValidConversionResult_PopulatesSummaryAndSteps()
    {
        // Arrange
        var dummyMesh = new CanonicalMesh(
            "MLOD_LOD0",
            new[] { new CanonicalVertex(new MeshVector3(0, 0, 0)) },
            new[] { new CanonicalFace(0, 0, 0) },
            null,
            CanonicalCoordinateSystem.RightHandedYUp,
            GameVersion.Sims3,
            null
        );

        var fakeInspectionService = new FakePackageInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeConversionService = new FakeConversionService(
            new DecorativeObjectConversionResult(
                IsSuccess: true,
                SourcePackagePath: "/source/onyx.package",
                TargetOutputPath: "/target/onyx_ts4.package",
                Plan: new DecorativeObjectConversionPlan(
                    SourcePackagePath: "/source/onyx.package",
                    TargetOutputPath: "/target/onyx_ts4.package",
                    TargetGameVersion: GameVersion.Sims4,
                    TotalMeshCandidateCount: 1,
                    TotalTextureCandidateCount: 1,
                    ConvertableMeshCount: 1,
                    ValidTextureCount: 1,
                    MeshCandidates: Array.Empty<MeshResourceRow>(),
                    TextureCandidates: Array.Empty<TextureResourceRow>(),
                    Steps: new[]
                    {
                        new DecorativeObjectConversionStep("STEP-01", "Validation", DecorativeObjectConversionStepStatus.Completed, "OK", Array.Empty<ConversionIssue>()),
                        new DecorativeObjectConversionStep("STEP-08-EXEC-CONV", "Execution", DecorativeObjectConversionStepStatus.Completed, "OK", Array.Empty<ConversionIssue>())
                    },
                    IsFeasible: true,
                    InputBundle: new DecorativeObjectConversionInputBundle(
                        SourcePackagePath: "/source/onyx.package",
                        TargetOutputPath: "/target/onyx_ts4.package",
                        TargetGameVersion: GameVersion.Sims4,
                        MeshBundles: new[]
                        {
                            new DecorativeObjectMeshInputBundle(
                                ResourceId: new PackageResourceId(0x015A1842, 0, 1),
                                FormattedKey: "015A1842:00000000:0000000000000001",
                                CanonicalMesh: dummyMesh,
                                RawPayload: new byte[] { 1, 2, 3, 4 }
                            )
                        },
                        TextureAssets: new[]
                        {
                            new DecorativeObjectSourceTextureAsset(
                                FormattedKey: "00B2D882:00000000:0000000000000002",
                                ResourceId: new PackageResourceId(0x00B2D882, 0, 2),
                                ClassificationKind: TextureClassificationKind.KnownTexture,
                                MapKind: TextureMapKind.Diffuse,
                                FormatName: "DDS",
                                CanExtractRawPayload: true,
                                Issues: Array.Empty<ConversionIssue>(),
                                RawPayload: new byte[] { 5, 6, 7, 8 }
                            )
                        },
                        ObjectModelDecomposition: null,
                        RigResources: Array.Empty<PackageResourceRow>(),
                        RsltResources: Array.Empty<PackageResourceRow>(),
                        ResourceLinks: new[]
                        {
                            new DecorativeObjectSourceResourceLink("015A1842:00000000:0000000000000001", "00B2D882:00000000:0000000000000002", "MeshTexture")
                        },
                        IsBundleValid: true,
                        Issues: Array.Empty<ConversionIssue>()
                    )
                ),
                Issues: Array.Empty<ConversionIssue>()
            )
        );

        var viewModel = new ResourceInspectorViewModel(
            fakeInspectionService,
            conversionService: fakeConversionService
        )
        {
            SelectedFilePath = "/source/onyx.package",
            TargetOutputPath = "/target/onyx_ts4.package"
        };

        // Act
        await viewModel.ConvertCommand.ExecuteAsync(null);

        // Assert
        viewModel.HasConversionResult.Should().BeTrue();
        viewModel.IsConversionSuccess.Should().BeTrue();
        viewModel.LastConvertedPackagePath.Should().Be("/target/onyx_ts4.package");
        viewModel.ConversionSteps.Should().HaveCount(2);
        viewModel.ConversionResourceLinks.Should().HaveCount(1);
        viewModel.ConversionMeshCount.Should().Be(1);
        viewModel.ConversionTextureCount.Should().Be(1);
        viewModel.ConversionTotalResourceCount.Should().Be(7); // 5 base + 1 mesh + 1 tex
        viewModel.StatusMessage.Should().Contain("Conversion completed successfully");
    }

    [Fact]
    public async Task ConvertAsync_GivenValidationFailure_SetsIsConversionSuccessFalseAndClearsLastConvertedPackagePath()
    {
        // Arrange
        var fakeInspectionService = new FakePackageInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeConversionService = new FakeConversionService(
            DecorativeObjectConversionResult.Failure(
                "/source/bad.package",
                "/target/bad_ts4.package",
                "VAL003",
                "MLOD payload compatibility verification failed."
            )
        );

        var viewModel = new ResourceInspectorViewModel(
            fakeInspectionService,
            conversionService: fakeConversionService
        )
        {
            SelectedFilePath = "/source/bad.package",
            TargetOutputPath = "/target/bad_ts4.package"
        };

        // Act
        await viewModel.ConvertCommand.ExecuteAsync(null);

        // Assert
        viewModel.HasConversionResult.Should().BeTrue();
        viewModel.IsConversionSuccess.Should().BeFalse();
        viewModel.LastConvertedPackagePath.Should().BeNull();
        viewModel.Issues.Should().ContainSingle(i => i.Code == "VAL003");
        viewModel.StatusMessage.Should().Contain("Conversion failed");
    }

    [Fact]
    public async Task InspectConvertedPackageAsync_WhenInvoked_SetsSelectedFilePathAndCallsInspect()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var stubResult = new PackageInspectionResult(
                true,
                tempFile,
                new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
                Array.Empty<PackageResourceRow>(),
                Array.Empty<ConversionIssue>()
            );

            var fakeInspectionService = new FakePackageInspectionService(stubResult);
            var viewModel = new ResourceInspectorViewModel(fakeInspectionService)
            {
                IsConversionSuccess = true,
                LastConvertedPackagePath = tempFile
            };

            // Act
            await viewModel.InspectConvertedPackageCommand.ExecuteAsync(null);

            // Assert
            viewModel.SelectedFilePath.Should().Be(tempFile);
            fakeInspectionService.InspectedFilePath.Should().Be(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task BrowseTargetOutputAsync_GivenPickedSavePath_UpdatesTargetOutputPath()
    {
        // Arrange
        var fakePicker = new FakeFilePickerService(null, null, "/picked/output.package");
        var fakeInspectionService = new FakePackageInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var viewModel = new ResourceInspectorViewModel(fakeInspectionService, filePickerService: fakePicker);

        // Act
        await viewModel.BrowseTargetOutputCommand.ExecuteAsync(null);

        // Assert
        viewModel.TargetOutputPath.Should().Be("/picked/output.package");
    }

    [Fact]
    public async Task ConvertAsync_GivenValidSims3PackMode_ExtractsPayloadAndExecutesConversion()
    {
        // Arrange
        var tempPackageFile = Path.GetTempFileName();
        try
        {
            var validPayload = new Sims3PackPayloadRow(
                EntryIndex: 0,
                Kind: Sims3PackPayloadKind.DbpfPackage.ToString(),
                DataOffset: 128,
                DataOffsetHex: "0x00000080",
                EstimatedSizeBytes: 1024,
                EstimatedSizeFormatted: "1 KB",
                DisplayName: "TestObject.package",
                CanExport: true,
                Issues: Array.Empty<ConversionIssue>()
            );

            var s3pResult = new Sims3PackInspectionResult(
                IsSuccess: true,
                FilePath: "/source/object.sims3pack",
                RootElementName: "Sims3Pack",
                DeclaredEncoding: "utf-8",
                RawXmlSizeBytes: 500,
                Title: "Test Object",
                AssetId: "123",
                AssetType: "Object",
                Description: "Desc",
                PayloadRows: new[] { validPayload },
                Issues: Array.Empty<ConversionIssue>()
            );

            var fakeInspectionService = new FakePackageInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
            var fakeS3pService = new FakeSims3PackInspectionService(
                s3pResult,
                new Sims3PackExportResult(true, "/source/object.sims3pack", tempPackageFile, 1024, Array.Empty<ConversionIssue>())
            );

            var fakeConversionService = new FakeConversionService(
                new DecorativeObjectConversionResult(
                    IsSuccess: true,
                    SourcePackagePath: tempPackageFile,
                    TargetOutputPath: "/target/object_ts4.package",
                    Plan: null,
                    Issues: Array.Empty<ConversionIssue>()
                )
            );

            var viewModel = new ResourceInspectorViewModel(
                fakeInspectionService,
                sims3PackInspectionService: fakeS3pService,
                conversionService: fakeConversionService
            )
            {
                SelectedFilePath = "/source/object.sims3pack",
                TargetOutputPath = "/target/object_ts4.package"
            };

            // Inspect Sims3Pack file to enter Sims3Pack mode
            await viewModel.InspectCommand.ExecuteAsync(null);

            viewModel.IsSims3PackMode.Should().BeTrue();
            viewModel.CanConvert.Should().BeTrue();

            // Act
            await viewModel.ConvertCommand.ExecuteAsync(null);

            // Assert
            viewModel.HasConversionResult.Should().BeTrue();
            viewModel.IsConversionSuccess.Should().BeTrue();
            viewModel.LastConvertedPackagePath.Should().Be("/target/object_ts4.package");
            fakeS3pService.ExportCalled.Should().BeTrue();
            viewModel.StatusMessage.Should().Contain("Conversion completed successfully");
        }
        finally
        {
            if (File.Exists(tempPackageFile))
            {
                File.Delete(tempPackageFile);
            }
        }
    }

    [Fact]
    public async Task CanConvert_InSims3PackMode_ReturnsFalseWhenSelectedPayloadCannotBeExported()
    {
        // Arrange
        var invalidPayload = new Sims3PackPayloadRow(
            EntryIndex: 0,
            Kind: Sims3PackPayloadKind.InvalidDbpfPackage.ToString(),
            DataOffset: 128,
            DataOffsetHex: "0x00000080",
            EstimatedSizeBytes: 1024,
            EstimatedSizeFormatted: "1 KB",
            DisplayName: "Invalid.package",
            CanExport: false,
            Issues: new[] { new ConversionIssue("ERR", "Corrupt payload", ConversionIssueSeverity.Error) }
        );

        var s3pResult = new Sims3PackInspectionResult(
            IsSuccess: true,
            FilePath: "/source/invalid.sims3pack",
            RootElementName: "Sims3Pack",
            DeclaredEncoding: "utf-8",
            RawXmlSizeBytes: 500,
            Title: "Invalid Object",
            AssetId: "123",
            AssetType: "Object",
            Description: "Desc",
            PayloadRows: new[] { invalidPayload },
            Issues: Array.Empty<ConversionIssue>()
        );

        var fakeInspectionService = new FakePackageInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeS3pService = new FakeSims3PackInspectionService(s3pResult);

        var viewModel = new ResourceInspectorViewModel(
            fakeInspectionService,
            sims3PackInspectionService: fakeS3pService
        )
        {
            SelectedFilePath = "/source/invalid.sims3pack",
            TargetOutputPath = "/target/invalid_ts4.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsSims3PackMode.Should().BeTrue();
        viewModel.CanConvert.Should().BeFalse();
    }

    [Fact]
    public void TargetGameVersion_WhenChanged_UpdatesDirectionTextButtonContentAndTargetSuffix()
    {
        // Arrange
        var fakeInspectionService = new FakePackageInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var viewModel = new ResourceInspectorViewModel(fakeInspectionService)
        {
            SelectedFilePath = "/path/to/my_object.package"
        };

        // Default state (TS3 -> TS4)
        viewModel.TargetGameVersion.Should().Be(GameVersion.Sims4);
        viewModel.ConversionDirectionText.Should().Be("TS3 -> TS4");
        viewModel.ConvertButtonContent.Should().Be("Convert TS3 -> TS4");
        viewModel.TargetOutputPath.Should().EndWith("_ts4.package");

        // Act: change to TS4 -> TS3
        viewModel.TargetGameVersion = GameVersion.Sims3;

        // Assert
        viewModel.TargetGameVersion.Should().Be(GameVersion.Sims3);
        viewModel.ConversionDirectionText.Should().Be("TS4 -> TS3");
        viewModel.ConvertButtonContent.Should().Be("Convert TS4 -> TS3");
        viewModel.TargetOutputPath.Should().EndWith("_ts3.package");
        viewModel.SelectedTargetGameVersionOption?.Version.Should().Be(GameVersion.Sims3);
    }

    [Fact]
    public async Task ConvertAsync_ReverseConversion_TS4ToTS3_PassesTargetGameVersionSims3ToService()
    {
        // Arrange
        var fakeInspectionService = new FakePackageInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeConversionService = new FakeConversionService(
            new DecorativeObjectConversionResult(
                IsSuccess: true,
                SourcePackagePath: "/source/ts4_chair.package",
                TargetOutputPath: "/target/ts4_chair_ts3.package",
                Plan: null,
                Issues: Array.Empty<ConversionIssue>()
            )
        );

        var viewModel = new ResourceInspectorViewModel(
            fakeInspectionService,
            conversionService: fakeConversionService
        )
        {
            SelectedFilePath = "/source/ts4_chair.package",
            TargetGameVersion = GameVersion.Sims3,
            TargetOutputPath = "/target/ts4_chair_ts3.package"
        };

        // Act
        await viewModel.ConvertCommand.ExecuteAsync(null);

        // Assert
        viewModel.HasConversionResult.Should().BeTrue();
        viewModel.IsConversionSuccess.Should().BeTrue();
        fakeConversionService.LastRequest.Should().NotBeNull();
        fakeConversionService.LastRequest!.TargetGameVersion.Should().Be(GameVersion.Sims3);
    }

    [Fact]
    public async Task InspectAsync_WhenInspectingTs4Package_RecommendsTs4ToTs3Direction()
    {
        // Arrange
        var ts4ResourceRow = new PackageResourceRow(
            TypeId: 0xC0DB5AE7,
            GroupId: 0,
            InstanceId: 1,
            TypeHex: "C0DB5AE7",
            GroupHex: "00000000",
            InstanceHex: "0000000000000001",
            FormattedKey: "C0DB5AE7:00000000:0000000000000001",
            Offset: 96,
            CompressedSize: 100,
            DecompressedSize: 200,
            CompressionKind: PackageCompressionKind.RefPack,
            CompressionName: "RefPack"
        );

        var ts4Result = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "/source/ts4_object.package",
            Header: new DbpfHeader("DBPF", 2, 1, 0, 96, 1),
            Resources: new[] { ts4ResourceRow },
            Issues: Array.Empty<ConversionIssue>()
        );

        var fakeInspectionService = new FakePackageInspectionService(ts4Result);
        var viewModel = new ResourceInspectorViewModel(fakeInspectionService)
        {
            SelectedFilePath = "/source/ts4_object.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.TargetGameVersion.Should().Be(GameVersion.Sims3);
        viewModel.ConversionDirectionText.Should().Be("TS4 -> TS3");
        viewModel.TargetOutputPath.Should().EndWith("_ts3.package");
    }

    [Fact]
    public async Task InspectAsync_WhenInspectingTs3Package_RecommendsTs3ToTs4Direction()
    {
        // Arrange
        var ts3ResourceRow = new PackageResourceRow(
            TypeId: 0x015A1842,
            GroupId: 0,
            InstanceId: 1,
            TypeHex: "015A1842",
            GroupHex: "00000000",
            InstanceHex: "0000000000000001",
            FormattedKey: "015A1842:00000000:0000000000000001",
            Offset: 96,
            CompressedSize: 100,
            DecompressedSize: 200,
            CompressionKind: PackageCompressionKind.Zlib,
            CompressionName: "ZLIB"
        );

        var ts3Result = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "/source/ts3_object.package",
            Header: new DbpfHeader("DBPF", 2, 0, 0, 96, 1),
            Resources: new[] { ts3ResourceRow },
            Issues: Array.Empty<ConversionIssue>()
        );

        var fakeInspectionService = new FakePackageInspectionService(ts3Result);
        var viewModel = new ResourceInspectorViewModel(fakeInspectionService)
        {
            SelectedFilePath = "/source/ts3_object.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.TargetGameVersion.Should().Be(GameVersion.Sims4);
        viewModel.ConversionDirectionText.Should().Be("TS3 -> TS4");
        viewModel.TargetOutputPath.Should().EndWith("_ts4.package");
    }

    [Fact]
    public async Task InspectAsync_WhenTs3PackageContainsSharedCobj_RecommendsTs3ToTs4Direction()
    {
        var sharedCobjRow = new PackageResourceRow(
            TypeId: 0x319E4F1D,
            GroupId: 0,
            InstanceId: 1,
            TypeHex: "319E4F1D",
            GroupHex: "00000000",
            InstanceHex: "0000000000000001",
            FormattedKey: "319E4F1D:00000000:0000000000000001",
            Offset: 96,
            CompressedSize: 100,
            DecompressedSize: 100,
            CompressionKind: PackageCompressionKind.None,
            CompressionName: "None"
        );

        var inspectionResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "/source/ts3_embedded.package",
            Header: new DbpfHeader("DBPF", 2, 1, 0, 96, 1),
            Resources: new[] { sharedCobjRow },
            Issues: Array.Empty<ConversionIssue>()
        );

        var viewModel = new ResourceInspectorViewModel(new FakePackageInspectionService(inspectionResult))
        {
            SelectedFilePath = "/source/ts3_embedded.package"
        };

        await viewModel.InspectCommand.ExecuteAsync(null);

        viewModel.TargetGameVersion.Should().Be(GameVersion.Sims4);
        viewModel.ConversionDirectionText.Should().Be("TS3 -> TS4");
        viewModel.TargetOutputPath.Should().EndWith("_ts4.package");
    }

    private sealed class FakePackageInspectionService : IPackageInspectionService
    {
        private readonly PackageInspectionResult _result;
        public string? InspectedFilePath { get; private set; }

        public FakePackageInspectionService(PackageInspectionResult result)
        {
            _result = result;
        }

        public Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            InspectedFilePath = filePath;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeSims3PackInspectionService : ISims3PackInspectionService
    {
        private readonly Sims3PackInspectionResult _inspectionResult;
        private readonly Sims3PackExportResult _exportResult;
        public bool ExportCalled { get; private set; }

        public FakeSims3PackInspectionService(Sims3PackInspectionResult inspectionResult, Sims3PackExportResult? exportResult = null)
        {
            _inspectionResult = inspectionResult;
            _exportResult = exportResult ?? Sims3PackExportResult.Failure("", "", "ERR", "Error");
        }

        public Task<Sims3PackInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_inspectionResult);
        }

        public Task<Sims3PackExportResult> ExportPayloadAsync(Sims3PackExportRequest request, CancellationToken cancellationToken = default)
        {
            ExportCalled = true;
            return Task.FromResult(_exportResult);
        }
    }

    private sealed class FakeConversionService : IDecorativeObjectConversionService
    {
        private readonly DecorativeObjectConversionResult _result;
        public DecorativeObjectConversionRequest? LastRequest { get; private set; }

        public FakeConversionService(DecorativeObjectConversionResult result)
        {
            _result = result;
        }

        public Task<DecorativeObjectConversionResult> CreateConversionPlanAsync(DecorativeObjectConversionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }

        public DecorativeObjectConversionResult CreateConversionPlan(DecorativeObjectConversionRequest request)
        {
            LastRequest = request;
            return _result;
        }

        public Task<DecorativeObjectConversionResult> ExecuteConversionAsync(DecorativeObjectConversionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }

        public DecorativeObjectConversionResult ExecuteConversion(DecorativeObjectConversionRequest request)
        {
            LastRequest = request;
            return _result;
        }
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        private readonly string? _pickedPackagePath;
        private readonly string? _pickedFolderPath;
        private readonly string? _savePackagePath;

        public FakeFilePickerService(string? pickedPackagePath, string? pickedFolderPath = null, string? savePackagePath = null)
        {
            _pickedPackagePath = pickedPackagePath;
            _pickedFolderPath = pickedFolderPath;
            _savePackagePath = savePackagePath;
        }

        public Task<string?> OpenPackageFilePickerAsync()
        {
            return Task.FromResult(_pickedPackagePath);
        }

        public Task<string?> OpenFolderPickerAsync()
        {
            return Task.FromResult(_pickedFolderPath);
        }

        public Task<string?> SavePackageFilePickerAsync()
        {
            return Task.FromResult(_savePackagePath);
        }
    }
}
