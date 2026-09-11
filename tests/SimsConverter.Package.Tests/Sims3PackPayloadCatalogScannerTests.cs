using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class Sims3PackPayloadCatalogScannerTests
{
    private readonly Sims3PackPayloadCatalogScanner _scanner = new();

    private static byte[] CreateSims3PackWithPayloadFixture(
        string xmlPayload = "<Sims3Pack><Title>Test</Title></Sims3Pack>",
        byte[]? archivePayload = null,
        uint? customXmlLength = null)
    {
        byte[] sigBytes = Encoding.ASCII.GetBytes("TS3Pack\0");
        uint sigLen = (uint)sigBytes.Length;
        ushort version = 1;
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlPayload);
        uint xmlLen = customXmlLength ?? (uint)xmlBytes.Length;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(sigLen);      // DWORD (LE)
        writer.Write(sigBytes);    // 8 bytes
        writer.Write(version);     // WORD (LE)
        writer.Write(xmlLen);      // DWORD (LE)
        writer.Write(xmlBytes);    // XML payload

        if (archivePayload != null && archivePayload.Length > 0)
        {
            writer.Write(archivePayload);
        }

        return ms.ToArray();
    }

    private static byte[] CreateValidDbpfHeaderBytes(int length = 200)
    {
        byte[] bytes = new byte[length];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(bytes, 0);
        BitConverter.GetBytes(2).CopyTo(bytes, 4); // Major 2
        BitConverter.GetBytes(1).CopyTo(bytes, 36); // 1 entry
        BitConverter.GetBytes(96).CopyTo(bytes, 40); // Index offset 96
        BitConverter.GetBytes(32).CopyTo(bytes, 44); // Index size 32
        return bytes;
    }

    [Fact]
    public async Task ScanFileAsync_GivenEmbeddedDbpfPayload_ReturnsDbpfCatalogEntryWithCorrectOffset()
    {
        // Arrange: XML section + valid DBPF package payload
        byte[] dbpfBytes = CreateValidDbpfHeaderBytes(200);

        string testPath = Path.Combine(Path.GetTempPath(), "dbpf_catalog_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: dbpfBytes);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().ContainSingle();
            result.Entries[0].Kind.Should().Be(Sims3PackPayloadKind.DbpfPackage);
            result.Entries[0].EntryIndex.Should().Be(1);

            int expectedArchiveOffset = 4 + 8 + 2 + 4 + Encoding.UTF8.GetByteCount("<Sims3Pack><Title>Test</Title></Sims3Pack>");
            result.Entries[0].DataOffset.Should().Be(expectedArchiveOffset);
            result.Entries[0].Issues.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_GivenInvalidDbpfCandidateWithZeroIndexEntries_ClassifiesAsInvalidDbpfPackage()
    {
        // Arrange: DBPF magic present, but indexEntryCount is 0 (INVALID!)
        byte[] invalidDbpfBytes = new byte[200];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(invalidDbpfBytes, 0);
        BitConverter.GetBytes(2).CopyTo(invalidDbpfBytes, 4);
        BitConverter.GetBytes(0).CopyTo(invalidDbpfBytes, 36); // 0 entries
        BitConverter.GetBytes(0).CopyTo(invalidDbpfBytes, 40); // Index offset 0
        BitConverter.GetBytes(0).CopyTo(invalidDbpfBytes, 44);

        string testPath = Path.Combine(Path.GetTempPath(), "invalid_dbpf_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: invalidDbpfBytes);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().ContainSingle();
            result.Entries[0].Kind.Should().Be(Sims3PackPayloadKind.InvalidDbpfPackage);
            result.Entries[0].Issues.Should().ContainSingle(i => i.Code == "S3PC010");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_GivenInvalidDbpfFollowedByValidDbpf_IdentifiesBothCorrectly()
    {
        // Arrange: Invalid DBPF candidate (0 entries) followed by valid DBPF candidate
        byte[] multiPayload = new byte[600];
        
        // Candidate 1 at offset 0 (Invalid: indexEntryCount 0)
        Encoding.ASCII.GetBytes("DBPF").CopyTo(multiPayload, 0);
        BitConverter.GetBytes(2).CopyTo(multiPayload, 4);
        BitConverter.GetBytes(0).CopyTo(multiPayload, 36);
        BitConverter.GetBytes(0).CopyTo(multiPayload, 40);

        // Candidate 2 at offset 250 (Valid: indexOffset 96, 1 entry)
        Encoding.ASCII.GetBytes("DBPF").CopyTo(multiPayload, 250 + 0);
        BitConverter.GetBytes(2).CopyTo(multiPayload, 250 + 4);
        BitConverter.GetBytes(1).CopyTo(multiPayload, 250 + 36);
        BitConverter.GetBytes(96).CopyTo(multiPayload, 250 + 40);
        BitConverter.GetBytes(32).CopyTo(multiPayload, 250 + 44);

        string testPath = Path.Combine(Path.GetTempPath(), "multi_invalid_valid_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: multiPayload);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().HaveCount(2);
            result.Entries[0].Kind.Should().Be(Sims3PackPayloadKind.InvalidDbpfPackage);
            result.Entries[1].Kind.Should().Be(Sims3PackPayloadKind.DbpfPackage);
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_GivenDbpfInChunkOverlapRegion_ReturnsSingleDeduplicatedEntry()
    {
        // Arrange: Place DBPF magic in overlap region near end of 8192 byte chunk (offset 8188)
        byte[] largePayload = new byte[16000];
        byte[] dbpfHeader = CreateValidDbpfHeaderBytes(200);
        Array.Copy(dbpfHeader, 0, largePayload, 8188, dbpfHeader.Length);

        string testPath = Path.Combine(Path.GetTempPath(), "overlap_dbpf_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: largePayload);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert: MUST produce exactly 1 deduplicated entry (not 2)
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().ContainSingle();
            result.Entries[0].Kind.Should().Be(Sims3PackPayloadKind.DbpfPackage);

            int expectedArchiveOffset = 4 + 8 + 2 + 4 + Encoding.UTF8.GetByteCount("<Sims3Pack><Title>Test</Title></Sims3Pack>");
            result.Entries[0].DataOffset.Should().Be(expectedArchiveOffset + 8188);
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_GivenPngSplitAcrossChunkBoundary_ReturnsSingleDeduplicatedEntry()
    {
        // Arrange: PNG magic split across 8192 chunk boundary (4 bytes at offset 8188, 4 bytes at 8192)
        byte[] largePayload = new byte[16000];
        byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Array.Copy(pngHeader, 0, largePayload, 8188, pngHeader.Length);

        string testPath = Path.Combine(Path.GetTempPath(), "overlap_png_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: largePayload);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert: MUST produce exactly 1 deduplicated entry
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().ContainSingle();
            result.Entries[0].Kind.Should().Be(Sims3PackPayloadKind.PngPreview);

            int expectedArchiveOffset = 4 + 8 + 2 + 4 + Encoding.UTF8.GetByteCount("<Sims3Pack><Title>Test</Title></Sims3Pack>");
            result.Entries[0].DataOffset.Should().Be(expectedArchiveOffset + 8188);
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_GivenMaxCatalogEntriesLimitReached_ReturnsControlledWarningIssue()
    {
        // Arrange: Create payload containing 120 DBPF candidates (> 100 limit)
        byte[] manyDbpfs = new byte[120 * 200];
        byte[] singleHeader = CreateValidDbpfHeaderBytes(200);
        for (int i = 0; i < 120; i++)
        {
            singleHeader.CopyTo(manyDbpfs, i * 200);
        }

        string testPath = Path.Combine(Path.GetTempPath(), "max_entries_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: manyDbpfs);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert: Exactly 100 entries returned + controlled warning issue S3PC008
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().HaveCount(Sims3PackPayloadCatalogScanner.MaxCatalogEntries);
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PC008");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanAsync_GivenNonSeekableStreamWithHugeXmlLength_ReturnsControlledFailureWithoutOom()
    {
        // Arrange: Non-seekable stream with huge xmlLength (500MB) -> MUST NOT allocate memory or throw OOM
        uint hugeXmlLength = 500 * 1024 * 1024;
        byte[] payload = CreateSims3PackWithPayloadFixture("<Sims3Pack/>", customXmlLength: hugeXmlLength);
        using var ms = new MemoryStream(payload);
        using var nonSeekableStream = new NonSeekableStreamWrapper(ms);

        // Act
        var result = await _scanner.ScanAsync(nonSeekableStream);

        // Assert: Controlled failure S3PC007 without OOM
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("S3PC007");
    }

    [Fact]
    public async Task ScanFileAsync_GivenMultipleDbpfCandidates_ReturnsEntriesOrderedByDataOffset()
    {
        // Arrange: 2 DBPF candidates separated by padding bytes
        byte[] multiPayload = new byte[600];
        byte[] validHeader1 = CreateValidDbpfHeaderBytes(200);
        byte[] validHeader2 = CreateValidDbpfHeaderBytes(200);
        validHeader1.CopyTo(multiPayload, 10);  // Candidate 1 at offset 10
        validHeader2.CopyTo(multiPayload, 300); // Candidate 2 at offset 300

        string testPath = Path.Combine(Path.GetTempPath(), "multi_dbpf_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: multiPayload);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().HaveCount(2);
            result.Entries[0].DataOffset.Should().BeLessThan(result.Entries[1].DataOffset);
            result.Entries[0].EntryIndex.Should().Be(1);
            result.Entries[1].EntryIndex.Should().Be(2);
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_GivenTruncatedDbpfCandidate_ReturnsInvalidEntryWithControlledIssue()
    {
        // Arrange: DBPF magic candidate followed by only 10 bytes (< 96 bytes)
        byte[] truncatedDbpfBytes = new byte[14];
        "DBPF"u8.ToArray().CopyTo(truncatedDbpfBytes, 0);

        string testPath = Path.Combine(Path.GetTempPath(), "truncated_dbpf_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(archivePayload: truncatedDbpfBytes);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Entries.Should().ContainSingle();
            result.Entries[0].Kind.Should().Be(Sims3PackPayloadKind.InvalidDbpfPackage);
            result.Entries[0].Issues.Should().ContainSingle(i => i.Code == "S3PC010");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_GivenXmlLengthExceedingStreamLength_ReturnsControlledFailure()
    {
        // Arrange: xmlLength set to 999999 bytes extending beyond file length
        string testPath = Path.Combine(Path.GetTempPath(), "bad_xml_len_scan_" + Guid.NewGuid() + ".sims3pack");
        byte[] fullPayload = CreateSims3PackWithPayloadFixture(customXmlLength: 999999);
        await File.WriteAllBytesAsync(testPath, fullPayload);

        try
        {
            // Act
            var result = await _scanner.ScanFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PC003");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    private sealed class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _inner;
        public NonSeekableStreamWrapper(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
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
