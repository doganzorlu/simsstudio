using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class Sims3PackXmlParserTests
{
    private readonly Sims3PackXmlParser _parser = new();

    private static byte[] CreateSims3PackFixture(
        string xmlPayload,
        string signature = "TS3Pack\0",
        byte[]? extraTrailingBytes = null,
        uint? customXmlLength = null)
    {
        byte[] sigBytes = Encoding.ASCII.GetBytes(signature);
        uint sigLen = (uint)sigBytes.Length;
        ushort version = 1;
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlPayload);
        uint xmlLen = customXmlLength ?? (uint)xmlBytes.Length;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(sigLen);      // DWORD (LE)
        writer.Write(sigBytes);    // sigLen bytes
        writer.Write(version);     // WORD (LE)
        writer.Write(xmlLen);      // DWORD (LE)
        writer.Write(xmlBytes);    // XML payload

        if (extraTrailingBytes != null && extraTrailingBytes.Length > 0)
        {
            writer.Write(extraTrailingBytes);
        }

        return ms.ToArray();
    }

    [Fact]
    public async Task ParseFileAsync_GivenValidSims3PackXml_ExtractsStructuralMetadataCorrectly()
    {
        // Arrange
        string xmlPayload = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Sims3Pack>
    <Title>Modern Villa</Title>
    <AssetId>GUID-12345-67890</AssetId>
    <AssetType>Lot</AssetType>
    <Description>A luxurious modern home.</Description>
    <Files>
        <Filename>villa_main.package</Filename>
        <Filename>villa_furniture.package</Filename>
    </Files>
</Sims3Pack>";

        string testPath = Path.Combine(Path.GetTempPath(), "valid_xml_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateSims3PackFixture(xmlPayload);
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Metadata.Should().NotBeNull();
            result.Metadata!.RootElementName.Should().Be("Sims3Pack");
            result.Metadata.DeclaredEncoding.Should().Be("utf-8");
            result.Metadata.RawXmlSizeBytes.Should().Be(Encoding.UTF8.GetByteCount(xmlPayload));
            result.Metadata.EmbeddedFileCount.Should().Be(2);
            result.Metadata.EmbeddedFileNames.Should().ContainInOrder("villa_main.package", "villa_furniture.package");
            result.Metadata.Title.Should().Be("Modern Villa");
            result.Metadata.AssetId.Should().Be("GUID-12345-67890");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ParseFileAsync_GivenNonTerminatedExact7ByteSignature_ReturnsSuccess()
    {
        // Arrange: Exact 7-byte signature "TS3Pack" without null terminator
        string xmlPayload = "<?xml version=\"1.0\"?><Sims3Pack><Title>7ByteSig</Title></Sims3Pack>";
        string testPath = Path.Combine(Path.GetTempPath(), "sig7_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateSims3PackFixture(xmlPayload, signature: "TS3Pack");
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Metadata!.RootElementName.Should().Be("Sims3Pack");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ParseFileAsync_GivenWhitespacePaddedSignature_ReturnsControlledFailureIssue()
    {
        // Arrange: Space-padded signature "TS3Pack  " MUST be rejected
        string xmlPayload = "<Sims3Pack/>";
        string testPath = Path.Combine(Path.GetTempPath(), "space_padded_sig_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateSims3PackFixture(xmlPayload, signature: "TS3Pack  ");
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PX005");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ParseFileAsync_GivenXmlLengthExceeding10MbLimit_ReturnsControlledFailureWithoutOom()
    {
        // Arrange: xmlLength set to 500MB (500 * 1024 * 1024)
        uint hugeXmlLength = 500 * 1024 * 1024;
        string testPath = Path.Combine(Path.GetTempPath(), "huge_xml_len_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateSims3PackFixture("<Sims3Pack/>", customXmlLength: hugeXmlLength);
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(testPath);

            // Assert: Must return controlled failure S3PX006 without attempting memory allocation
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PX006");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ParseAsync_GivenNonSeekableStreamWithHugeXmlLength_ReturnsControlledFailureWithoutOom()
    {
        // Arrange: Non-seekable stream with huge xmlLength
        uint hugeXmlLength = 200 * 1024 * 1024; // 200MB
        byte[] payload = CreateSims3PackFixture("<Sims3Pack/>", customXmlLength: hugeXmlLength);
        using var ms = new MemoryStream(payload);
        using var nonSeekableStream = new NonSeekableStreamWrapper(ms);

        // Act
        var result = await _parser.ParseAsync(nonSeekableStream);

        // Assert: Must return controlled failure S3PX006 without OOM exception
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("S3PX006");
    }

    [Fact]
    public async Task ParseAsync_ReadsExactlyXmlLengthBytes_IgnoringExtraTrailingBytes()
    {
        // Arrange: Stream contains XML metadata + 512 bytes of extra binary payload
        string xmlPayload = "<Sims3Pack><Title>Exact Boundary</Title><AssetId>100</AssetId></Sims3Pack>";
        byte[] extraBytes = new byte[512];
        new Random(42).NextBytes(extraBytes);

        byte[] payload = CreateSims3PackFixture(xmlPayload, extraTrailingBytes: extraBytes);
        using var stream = new MemoryStream(payload);

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata!.RawXmlSizeBytes.Should().Be(Encoding.UTF8.GetByteCount(xmlPayload));

        int headerLength = 4 + 8 + 2 + 4; // 18 bytes
        int expectedPos = headerLength + Encoding.UTF8.GetByteCount(xmlPayload);
        stream.Position.Should().Be(expectedPos);
    }

    [Fact]
    public async Task ParseFileAsync_GivenMalformedXml_ReturnsControlledFailureIssue()
    {
        // Arrange: Invalid XML syntax
        string malformedXml = "<Sims3Pack><Title>Broken XML<AssetId>Unclosed Tag</Sims3Pack>";
        string testPath = Path.Combine(Path.GetTempPath(), "malformed_xml_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateSims3PackFixture(malformedXml);
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PX001");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ParseFileAsync_GivenXxeAttackPayload_IsSafelyRejectedWithoutException()
    {
        // Arrange: XXE attack payload attempting to access local file system entity
        string xxeXml = @"<?xml version=""1.0""?>
<!DOCTYPE foo [
  <!ELEMENT foo ANY >
  <!ENTITY xxe SYSTEM ""file:///etc/passwd"" >]>
<Sims3Pack><Title>&xxe;</Title></Sims3Pack>";

        string testPath = Path.Combine(Path.GetTempPath(), "xxe_xml_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateSims3PackFixture(xxeXml);
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(testPath);

            // Assert: Secure XmlReader Settings MUST prohibit DTD or handle safely without uncaught exception
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PX001");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ParseFileAsync_GivenEmptyXmlSection_ReturnsControlledIssue()
    {
        // Arrange: xmlLength = 0 or whitespace payload
        string testPath = Path.Combine(Path.GetTempPath(), "empty_xml_" + Guid.NewGuid() + ".sims3pack");
        byte[] payload = CreateSims3PackFixture("   \r\n   ");
        await File.WriteAllBytesAsync(testPath, payload);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PX002");
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
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
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
