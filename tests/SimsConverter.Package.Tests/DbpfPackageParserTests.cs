using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class DbpfPackageParserTests
{
    private readonly DbpfPackageParser _parser = new();

    [Fact]
    public void Parse_GivenValidDbpf2HeaderAndOneResourceEntry_ReturnsSuccessfulResult()
    {
        // Arrange: DBPF 2.0 package buffer with header + 1 entry at offset 96
        byte[] buffer = new byte[96 + 32];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4); // Major 2
        BitConverter.GetBytes(0).CopyTo(buffer, 8); // Minor 0
        BitConverter.GetBytes(1).CopyTo(buffer, 36); // 1 entry
        BitConverter.GetBytes(96).CopyTo(buffer, 40); // Index offset 96
        BitConverter.GetBytes(32).CopyTo(buffer, 44); // Index size 32

        // Entry at offset 96
        BitConverter.GetBytes(0x00B2D882u).CopyTo(buffer, 96 + 0); // TypeId
        BitConverter.GetBytes(0x00000000u).CopyTo(buffer, 96 + 4); // GroupId
        BitConverter.GetBytes(0x123456789ABCDEF0UL).CopyTo(buffer, 96 + 8); // InstanceId
        BitConverter.GetBytes(500u).CopyTo(buffer, 96 + 16); // DataOffset
        BitConverter.GetBytes(1024u).CopyTo(buffer, 96 + 20); // CompressedSize
        BitConverter.GetBytes(1024u).CopyTo(buffer, 96 + 24); // DecompressedSize
        BitConverter.GetBytes((ushort)0x0000).CopyTo(buffer, 96 + 28); // Uncompressed

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Header.Should().NotBeNull();
        result.Header!.Magic.Should().Be("DBPF");
        result.Header.MajorVersion.Should().Be(2);
        result.Header.IndexEntryCount.Should().Be(1);
        result.Header.IndexOffset.Should().Be(96);

        result.Entries.Should().ContainSingle();
        var entry = result.Entries[0];
        entry.Id.TypeId.Should().Be(0x00B2D882u);
        entry.Id.GroupId.Should().Be(0x00000000u);
        entry.Id.InstanceId.Should().Be(0x123456789ABCDEF0UL);
        entry.Id.FormattedKey.Should().Be("00B2D882:00000000:123456789ABCDEF0");
        entry.DataOffset.Should().Be(500);
        entry.CompressedSize.Should().Be(1024);
        entry.DecompressedSize.Should().Be(1024);
        entry.CompressionKind.Should().Be(PackageCompressionKind.None);
    }

    [Fact]
    public void Parse_GivenValidDbpf2HeaderAndMultipleResourceEntries_ReturnsAllEntries()
    {
        // Arrange: DBPF 2.0 package with 2 index entries
        int entryCount = 2;
        int indexOffset = 96;
        byte[] buffer = new byte[indexOffset + (entryCount * 32)];

        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(entryCount).CopyTo(buffer, 36);
        BitConverter.GetBytes(indexOffset).CopyTo(buffer, 40);
        BitConverter.GetBytes(entryCount * 32).CopyTo(buffer, 44);

        // Entry 0 (Compressed ZLIB)
        BitConverter.GetBytes(0x01u).CopyTo(buffer, 96 + 0);
        BitConverter.GetBytes(0x02u).CopyTo(buffer, 96 + 4);
        BitConverter.GetBytes(0x1000UL).CopyTo(buffer, 96 + 8);
        BitConverter.GetBytes(200u).CopyTo(buffer, 96 + 16);
        BitConverter.GetBytes(100u).CopyTo(buffer, 96 + 20);
        BitConverter.GetBytes(250u).CopyTo(buffer, 96 + 24);
        BitConverter.GetBytes((ushort)0x5A42).CopyTo(buffer, 96 + 28); // ZLIB

        // Entry 1 (RefPack)
        BitConverter.GetBytes(0x03u).CopyTo(buffer, 128 + 0);
        BitConverter.GetBytes(0x04u).CopyTo(buffer, 128 + 4);
        BitConverter.GetBytes(0x2000UL).CopyTo(buffer, 128 + 8);
        BitConverter.GetBytes(500u).CopyTo(buffer, 128 + 16);
        BitConverter.GetBytes(300u).CopyTo(buffer, 128 + 20);
        BitConverter.GetBytes(600u).CopyTo(buffer, 128 + 24);
        BitConverter.GetBytes((ushort)0xFFFE).CopyTo(buffer, 128 + 28); // RefPack

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Entries.Should().HaveCount(2);
        result.Entries[0].CompressionKind.Should().Be(PackageCompressionKind.Zlib);
        result.Entries[1].CompressionKind.Should().Be(PackageCompressionKind.RefPack);
    }

    [Fact]
    public void Parse_GivenMagicOnlyBuffer4Bytes_ReturnsTruncatedHeaderWarning_WithoutCrashing()
    {
        // Arrange: Exactly 4 bytes "DBPF" (no major/minor version or header fields)
        byte[] magicOnlyBuffer = Encoding.ASCII.GetBytes("DBPF");

        // Act
        var result = _parser.Parse(magicOnlyBuffer);

        // Assert: Must not throw ArgumentOutOfRangeException or crash
        result.IsSuccess.Should().BeFalse();
        result.Header.Should().NotBeNull();
        result.Header!.Magic.Should().Be("DBPF");
        result.Header.MajorVersion.Should().Be(0);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE003");
    }

    [Theory]
    [InlineData(5)] // DBPF + 1 byte
    [InlineData(6)] // DBPF + 2 bytes
    [InlineData(7)] // DBPF + 3 bytes
    public void Parse_GivenDbpfPlus1To3Bytes_ReturnsTruncatedHeaderWarning_WithoutCrashing(int bufferLength)
    {
        // Arrange: DBPF magic + partial version bytes (5..7 bytes total)
        byte[] truncatedBuffer = new byte[bufferLength];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(truncatedBuffer, 0);

        // Act
        var result = _parser.Parse(truncatedBuffer);

        // Assert: Must not crash on Slice(4, 4)
        result.IsSuccess.Should().BeFalse();
        result.Header.Should().NotBeNull();
        result.Header!.Magic.Should().Be("DBPF");
        result.Header.MajorVersion.Should().Be(0);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE003");
    }

    [Fact]
    public void Parse_GivenInvalidMagic_ReturnsControlledFailure_WithoutCrashing()
    {
        // Arrange
        byte[] buffer = Encoding.ASCII.GetBytes("INVALID_MAGIC_HEADER_BYTES_1234");

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Header.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE002");
    }

    [Fact]
    public void Parse_GivenTruncatedHeader_ReturnsControlledFailure_WithoutCrashing()
    {
        // Arrange: 2 bytes only
        byte[] shortBuffer = new byte[] { 0x44, 0x42 }; // "DB"

        // Act
        var result = _parser.Parse(shortBuffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Header.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE001");
    }

    [Fact]
    public void Parse_GivenInvalidNegativeIndexCount_ReturnsControlledFailure()
    {
        // Arrange
        byte[] buffer = new byte[96];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(-5).CopyTo(buffer, 36); // Negative index count
        BitConverter.GetBytes(96).CopyTo(buffer, 40);

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE004");
    }

    [Fact]
    public void Parse_GivenIndexSizeBytesSmallerThanRequiredEntries_ReturnsControlledError()
    {
        // Arrange: DBPF 2.0 header stating 1 entry (requires 32 bytes), but IndexSizeBytes is set to 1 byte
        byte[] buffer = new byte[96 + 32];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(1).CopyTo(buffer, 36); // 1 entry
        BitConverter.GetBytes(96).CopyTo(buffer, 40); // Offset 96
        BitConverter.GetBytes(1).CopyTo(buffer, 44); // IndexSizeBytes = 1 (inconsistent!)

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE013");
        result.Issues[0].Severity.Should().Be(ConversionIssueSeverity.Error);
    }

    [Fact]
    public void Parse_GivenIndexOffsetOutsideBuffer_ReturnsControlledWarning_WithoutCrashing()
    {
        // Arrange: Header states index is at offset 500, but buffer is only 96 bytes long
        byte[] buffer = new byte[96];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(1).CopyTo(buffer, 36);
        BitConverter.GetBytes(500).CopyTo(buffer, 40); // Offset outside buffer
        BitConverter.GetBytes(32).CopyTo(buffer, 44);

        // Act
        var result = _parser.Parse(buffer);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Partial parse with warning
        result.Entries.Should().BeEmpty();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE007");
    }

    [Fact]
    public async Task ParseAsync_GivenNonSeekableStreamWithForwardOffset_ReadsIndexEntriesSuccessfully()
    {
        // Arrange: Create a non-seekable stream wrapper containing 128 bytes (header + entry at offset 128)
        byte[] buffer = new byte[128 + 32];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(1).CopyTo(buffer, 36);
        BitConverter.GetBytes(128).CopyTo(buffer, 40); // Index offset at 128 (after 32 bytes padding)
        BitConverter.GetBytes(32).CopyTo(buffer, 44);

        // Entry at offset 128
        BitConverter.GetBytes(0x00AABBCCu).CopyTo(buffer, 128 + 0);
        BitConverter.GetBytes(0x00000000u).CopyTo(buffer, 128 + 4);
        BitConverter.GetBytes(0x5555555555555555UL).CopyTo(buffer, 128 + 8);

        using var memStream = new MemoryStream(buffer);
        using var nonSeekableStream = new NonSeekableStreamWrapper(memStream);

        // Act
        var result = await _parser.ParseAsync(nonSeekableStream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Entries.Should().ContainSingle();
        result.Entries[0].Id.TypeId.Should().Be(0x00AABBCCu);
    }

    [Fact]
    public async Task ParseAsync_GivenNonSeekableStreamWithBackwardOffset_ReturnsControlledFailure()
    {
        // Arrange: Non-seekable stream with index offset 10 (less than 96 header bytes read)
        byte[] buffer = new byte[96];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(1).CopyTo(buffer, 36);
        BitConverter.GetBytes(10).CopyTo(buffer, 40); // Offset 10 (backwards seek required!)
        BitConverter.GetBytes(32).CopyTo(buffer, 44);

        using var memStream = new MemoryStream(buffer);
        using var nonSeekableStream = new NonSeekableStreamWrapper(memStream);

        // Act
        var result = await _parser.ParseAsync(nonSeekableStream);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PARSE014");
    }

    [Fact]
    public async Task ParseFileAsync_GivenValidPackageFile_ParsesSuccessfully_AndLeavesSourceFileIntact()
    {
        // Arrange: Write temporary DBPF file
        string tempPath = Path.Combine(Path.GetTempPath(), "parser_test_" + Guid.NewGuid() + ".package");
        byte[] buffer = new byte[96 + 32];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(1).CopyTo(buffer, 36);
        BitConverter.GetBytes(96).CopyTo(buffer, 40);
        BitConverter.GetBytes(32).CopyTo(buffer, 44);

        // Resource Entry
        BitConverter.GetBytes(0xE8F40422u).CopyTo(buffer, 96 + 0); // TypeId
        BitConverter.GetBytes(0x00000000u).CopyTo(buffer, 96 + 4);
        BitConverter.GetBytes(0x9876543210ABCDEFUL).CopyTo(buffer, 96 + 8);

        await File.WriteAllBytesAsync(tempPath, buffer);

        try
        {
            // Act
            var result = await _parser.ParseFileAsync(tempPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Header.Should().NotBeNull();
            result.Header!.IndexEntryCount.Should().Be(1);
            result.Entries.Should().ContainSingle();
            result.Entries[0].Id.TypeId.Should().Be(0xE8F40422u);

            // Source file check
            File.Exists(tempPath).Should().BeTrue();
            byte[] afterBytes = await File.ReadAllBytesAsync(tempPath);
            afterBytes.Should().Equal(buffer);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _inner;
        public NonSeekableStreamWrapper(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false; // Explicitly non-seekable
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
