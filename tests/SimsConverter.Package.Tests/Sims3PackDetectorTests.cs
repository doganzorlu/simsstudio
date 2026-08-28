using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class Sims3PackDetectorTests
{
    private readonly Sims3PackDetector _detector = new();

    private static byte[] CreateValidSims3PackFixture(
        string signature = "TS3Pack\0",
        string xmlPayload = "<?xml version=\"1.0\"?><Sims3Pack/>",
        uint? customXmlLength = null)
    {
        byte[] sigBytes = Encoding.ASCII.GetBytes(signature);
        uint sigLen = (uint)sigBytes.Length;
        ushort version = 1;
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlPayload);
        uint xmlLen = customXmlLength ?? (uint)xmlBytes.Length;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(sigLen);      // DWORD 4 bytes (LE)
        writer.Write(sigBytes);    // sigLen bytes
        writer.Write(version);     // WORD 2 bytes (LE)
        writer.Write(xmlLen);      // DWORD 4 bytes (LE)
        writer.Write(xmlBytes);    // XML payload

        return ms.ToArray();
    }

    [Fact]
    public async Task DetectFileAsync_GivenValidBinarySims3PackHeader_ReturnsHighConfidenceSims3Pack()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "valid_binary_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateValidSims3PackFixture("TS3Pack\0");
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
            result.Confidence.Should().Be(PackageDetectionConfidence.High);
            result.MajorVersion.Should().Be("1");
            result.Issues.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectFileAsync_GivenValidBinarySims3PackWithoutNullTerminator_ReturnsHighConfidenceSims3Pack()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "valid_nonull_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateValidSims3PackFixture("TS3Pack"); // Exactly 7 bytes
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
            result.Confidence.Should().Be(PackageDetectionConfidence.High);
            result.MajorVersion.Should().Be("1");
            result.Issues.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectFileAsync_GivenTS3PackInvalidSignature_ReturnsLowConfidenceWithIssue()
    {
        // Arrange: Signature spoof test - TS3PackInvalid signature MUST NOT return High confidence
        string testPath = Path.Combine(Path.GetTempPath(), "spoofed_sig_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateValidSims3PackFixture("TS3PackInvalid");
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.Confidence.Should().Be(PackageDetectionConfidence.Low);
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("DET006");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectFileAsync_GivenXmlLengthExceedingStreamLength_ReturnsLowConfidenceWithIssue()
    {
        // Arrange: Bad xmlLength beyond stream length test
        string testPath = Path.Combine(Path.GetTempPath(), "bad_xmllen_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateValidSims3PackFixture("TS3Pack\0", "<?xml version=\"1.0\"?><Sims3Pack/>", customXmlLength: 999999);
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.Confidence.Should().Be(PackageDetectionConfidence.Low);
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("DET011");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectFileAsync_GivenPlainXmlTextWithoutBinaryHeader_ReturnsLowConfidenceWithWarning()
    {
        // Arrange: Negative test — XML text only without TS3Pack binary header
        string testPath = Path.Combine(Path.GetTempPath(), "plain_xml_" + Guid.NewGuid() + ".sims3pack");
        string xmlContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Sims3Pack><Asset id=\"12345\"/></Sims3Pack>";
        await File.WriteAllTextAsync(testPath, xmlContent, Encoding.UTF8);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert: MUST NOT be High confidence
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.Confidence.Should().Be(PackageDetectionConfidence.Low);
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("DET008");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectFileAsync_GivenEmptyFileWithSims3PackExtension_ReturnsControlledIssue()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "empty_" + Guid.NewGuid() + ".sims3pack");
        await File.WriteAllBytesAsync(testPath, new byte[2]); // 2 bytes < 4 bytes

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.Confidence.Should().Be(PackageDetectionConfidence.None);
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("DET004");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectFileAsync_GivenSims3PackExtension_WithDbpfMagic_ReturnsDbpfKindWithWarning()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "dbpf_masquerade_" + Guid.NewGuid() + ".sims3pack");
        byte[] dbpfPayload = "DBPF\x02\x00\x00\x00"u8.ToArray();
        await File.WriteAllBytesAsync(testPath, dbpfPayload);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Dbpf);
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("DET005");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectFileAsync_GivenSims3PackExtension_WithUnrecognizedHeader_ReturnsLowConfidenceWithIssue()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "unknown_header_" + Guid.NewGuid() + ".sims3pack");
        byte[] randomBuffer = new byte[64];
        new Random(42).NextBytes(randomBuffer);
        await File.WriteAllBytesAsync(testPath, randomBuffer);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.Confidence.Should().Be(PackageDetectionConfidence.Low);
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("DET006");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task DetectAsync_GivenNonSeekableStream_ExecutesWithoutException_AndReturnsLowConfidenceWithInfo()
    {
        // Arrange
        byte[] binaryPayload = CreateValidSims3PackFixture();
        using var memoryStream = new MemoryStream(binaryPayload);
        using var nonSeekableStream = new NonSeekableStreamWrapper(memoryStream);

        // Act
        var result = await _detector.DetectAsync(nonSeekableStream, "sample.sims3pack");

        // Assert: MUST NOT throw exception and MUST NOT produce false High confidence
        result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
        result.Confidence.Should().Be(PackageDetectionConfidence.Low);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("DET012");
    }

    [Fact]
    public async Task PackageDetector_DelegatesSims3PackFilesToSims3PackDetector()
    {
        // Arrange
        var packageDetector = new PackageDetector(_detector);
        string testPath = Path.Combine(Path.GetTempPath(), "delegated_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateValidSims3PackFixture();
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await packageDetector.DetectFileAsync(testPath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Sims3Pack);
            result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
            result.Confidence.Should().Be(PackageDetectionConfidence.High);
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    private sealed class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public NonSeekableStreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException("Non-seekable stream does not support Length");
        public override long Position
        {
            get => throw new NotSupportedException("Non-seekable stream does not support Position get");
            set => throw new NotSupportedException("Non-seekable stream does not support Position set");
        }

        public override void Flush() => _baseStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken) => _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default) => _baseStream.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
