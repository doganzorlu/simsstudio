using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Services;

public class Ts3GeomMetadataReader : ITs3GeomMetadataReader
{
    private const uint RcolMagicLe = 0x4C4F4352; // "RCOL" ASCII in Little-Endian
    private const uint GeomMagicLe = 0x4D4F4547; // "GEOM" ASCII in Little-Endian
    private const uint MtnfMagicLe = 0x464E544D; // "MTNF" ASCII in Little-Endian
    private const uint GeomTypeIdLe = 0x015A1849;
    private const int MinRawGeomHeaderSizeBytes = 16;

    private readonly struct RcolLayoutCandidate
    {
        public bool IsValid { get; }
        public uint RcolVersion { get; }
        public uint HeaderSize { get; }
        public uint ExternalTgiCount { get; }
        public uint InternalResourceCount { get; }
        public int GeomInternalIndex { get; }
        public int GeomChunkOffset { get; }
        public uint GeomChunkSize { get; }
        public string ContainerFormat { get; }

        public RcolLayoutCandidate(
            bool isValid,
            uint rcolVersion,
            uint headerSize,
            uint externalTgiCount,
            uint internalResourceCount,
            int geomInternalIndex,
            int geomChunkOffset,
            uint geomChunkSize,
            string containerFormat)
        {
            IsValid = isValid;
            RcolVersion = rcolVersion;
            HeaderSize = headerSize;
            ExternalTgiCount = externalTgiCount;
            InternalResourceCount = internalResourceCount;
            GeomInternalIndex = geomInternalIndex;
            GeomChunkOffset = geomChunkOffset;
            GeomChunkSize = geomChunkSize;
            ContainerFormat = containerFormat;
        }
    }

    public Ts3GeomParseResult Read(ReadOnlySpan<byte> buffer)
    {
        var issues = new List<ConversionIssue>();

        if (buffer.IsEmpty || buffer.Length < MinRawGeomHeaderSizeBytes)
        {
            issues.Add(new ConversionIssue(
                "GEOM000",
                $"GEOM payload buffer is too short. Expected at least {MinRawGeomHeaderSizeBytes} bytes, actual length: {buffer.Length}.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        // 1. Raw Chunk Mode (starts directly with "GEOM" magic at offset 0)
        uint offset0Magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(0, 4));
        if (offset0Magic == GeomMagicLe)
        {
            return ParseGeomChunkReferenceLayout(buffer, chunkOffset: 0, chunkSize: (uint)buffer.Length, containerFormat: "RawChunk", rcolVersion: 0, externalTgiCount: 0, internalTgiCount: 1, issues);
        }

        // 2. Candidate Layout Validation Strategy for RCOL Container
        // Candidate A: Count-First Direct Header (geom_write.py default: externalTgiCount at offset 0, internalResourceCount at offset 4)
        var candidateA = EvaluateLayoutCandidate(buffer, rcolVersion: 3, headerSize: 8, extCountOffset: 0, intCountOffset: 4, payloadStartPos: 8, "CountFirstRCOL");
        if (candidateA.IsValid)
        {
            return ExecuteRcolParse(buffer, candidateA, issues);
        }

        // Candidate B: "RCOL" Tag Header ("RCOL" 4B, version 4B, headerSize 4B, extCount 4B, intCount 4B)
        if (offset0Magic == RcolMagicLe)
        {
            var candidateB = EvaluateLayoutCandidate(buffer, rcolVersion: BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)), headerSize: BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4)), extCountOffset: 12, intCountOffset: 16, payloadStartPos: 20, "TaggedRCOL");
            if (candidateB.IsValid)
            {
                return ExecuteRcolParse(buffer, candidateB, issues);
            }
        }

        // Candidate C: Numeric Version Header (version 4B, headerSize 4B, extCount 4B, intCount 4B)
        if (offset0Magic >= 1 && offset0Magic <= 10)
        {
            var candidateC = EvaluateLayoutCandidate(buffer, rcolVersion: offset0Magic, headerSize: BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4)), extCountOffset: 8, intCountOffset: 12, payloadStartPos: 16, "NumericVersionRCOL");
            if (candidateC.IsValid)
            {
                return ExecuteRcolParse(buffer, candidateC, issues);
            }
        }

        issues.Add(new ConversionIssue(
            "GEOM001",
            "Internal GEOM chunk signature 'GEOM' was not found in candidate RCOL container location tables.",
            ConversionIssueSeverity.Error
        ));
        return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
    }

    private static RcolLayoutCandidate EvaluateLayoutCandidate(
        ReadOnlySpan<byte> buffer,
        uint rcolVersion,
        uint headerSize,
        int extCountOffset,
        int intCountOffset,
        int payloadStartPos,
        string containerFormat)
    {
        if (intCountOffset + 4 > buffer.Length || payloadStartPos > buffer.Length)
        {
            return default;
        }

        uint extCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(extCountOffset, 4));
        uint intCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(intCountOffset, 4));

        if (intCount == 0 || intCount > 1000 || extCount > 1000)
        {
            return default;
        }

        int currPos = payloadStartPos;

        // 1. Internal ITG Array FIRST (16 bytes per entry: InstanceId 8B, TypeId 4B, GroupId 4B)
        int geomIntIndex = -1;
        try
        {
            checked
            {
                int intTgiBytes = (int)intCount * 16;
                if (currPos + intTgiBytes > buffer.Length)
                {
                    return default;
                }

                for (int i = 0; i < intCount; i++)
                {
                    int entryOffset = currPos + i * 16;
                    uint typeId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(entryOffset + 8, 4));
                    if (typeId == GeomTypeIdLe)
                    {
                        geomIntIndex = i;
                        break;
                    }
                }
                currPos += intTgiBytes;
            }
        }
        catch (OverflowException)
        {
            return default;
        }

        if (geomIntIndex < 0)
        {
            return default;
        }

        // 2. External Resource Array SECOND (16 bytes per entry)
        try
        {
            checked
            {
                int extTgiBytes = (int)extCount * 16;
                if (currPos + extTgiBytes > buffer.Length)
                {
                    return default;
                }
                currPos += extTgiBytes;
            }
        }
        catch (OverflowException)
        {
            return default;
        }

        // 3. Chunk Location Table THIRD (8 bytes per entry: Position uint32, Size uint32)
        int geomChunkOffset = -1;
        uint geomChunkSize = 0;

        try
        {
            checked
            {
                int locationTableBytes = (int)intCount * 8;
                if (currPos + locationTableBytes > buffer.Length)
                {
                    return default;
                }

                int entryOffset = currPos + geomIntIndex * 8;
                uint position = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(entryOffset, 4));
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(entryOffset + 4, 4));

                int candidateOffsetRel = (int)(position + headerSize);
                if (candidateOffsetRel >= 0 && candidateOffsetRel + 4 <= buffer.Length)
                {
                    if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(candidateOffsetRel, 4)) == GeomMagicLe)
                    {
                        geomChunkOffset = candidateOffsetRel;
                        geomChunkSize = size;
                    }
                }

                if (geomChunkOffset < 0 && position < buffer.Length && position + 4 <= buffer.Length)
                {
                    if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice((int)position, 4)) == GeomMagicLe)
                    {
                        geomChunkOffset = (int)position;
                        geomChunkSize = size;
                    }
                }
            }
        }
        catch (OverflowException)
        {
            return default;
        }

        if (geomChunkOffset < 0 || geomChunkOffset + 16 > buffer.Length)
        {
            return default;
        }

        return new RcolLayoutCandidate(
            isValid: true,
            rcolVersion: rcolVersion,
            headerSize: headerSize,
            externalTgiCount: extCount,
            internalResourceCount: intCount,
            geomInternalIndex: geomIntIndex,
            geomChunkOffset: geomChunkOffset,
            geomChunkSize: geomChunkSize,
            containerFormat: containerFormat
        );
    }

    private static Ts3GeomParseResult ExecuteRcolParse(
        ReadOnlySpan<byte> buffer,
        RcolLayoutCandidate candidate,
        List<ConversionIssue> issues)
    {
        if (candidate.GeomChunkSize > 0)
        {
            try
            {
                checked
                {
                    long totalChunkBoundary = (long)candidate.GeomChunkOffset + candidate.GeomChunkSize;
                    if (totalChunkBoundary > buffer.Length)
                    {
                        issues.Add(new ConversionIssue(
                            "GEOM004",
                            $"Out-of-bounds GEOM chunk location: offset {candidate.GeomChunkOffset} + size {candidate.GeomChunkSize} = {totalChunkBoundary} exceeds buffer length {buffer.Length}.",
                            ConversionIssueSeverity.Error
                        ));
                        return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                    }
                }
            }
            catch (OverflowException)
            {
                issues.Add(new ConversionIssue(
                    "GEOM004",
                    "Overflow verifying GEOM chunk location bounds.",
                    ConversionIssueSeverity.Error
                ));
                return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
            }
        }

        return ParseGeomChunkReferenceLayout(
            buffer,
            candidate.GeomChunkOffset,
            candidate.GeomChunkSize,
            candidate.ContainerFormat,
            candidate.RcolVersion,
            candidate.ExternalTgiCount,
            candidate.InternalResourceCount,
            issues
        );
    }

    private static Ts3GeomParseResult ParseGeomChunkReferenceLayout(
        ReadOnlySpan<byte> buffer,
        int chunkOffset,
        uint chunkSize,
        string containerFormat,
        uint rcolVersion,
        uint externalTgiCount,
        uint internalTgiCount,
        List<ConversionIssue> issues)
    {
        int effectiveChunkSize = chunkSize > 0 ? (int)chunkSize : (buffer.Length - chunkOffset);
        if (chunkOffset < 0 || chunkOffset + effectiveChunkSize > buffer.Length || effectiveChunkSize < 16)
        {
            issues.Add(new ConversionIssue(
                "GEOM005",
                $"Truncated GEOM chunk slice at offset {chunkOffset} (size {effectiveChunkSize}). Expected at least 16 bytes.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        ReadOnlySpan<byte> chunkSpan = buffer.Slice(chunkOffset, effectiveChunkSize);
        int curr = 0;

        // Magic "GEOM"
        uint magicUint = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        if (magicUint != GeomMagicLe)
        {
            string magicAscii = Encoding.ASCII.GetString(chunkSpan.Slice(curr, 4));
            issues.Add(new ConversionIssue(
                "GEOM001",
                $"Invalid GEOM chunk magic signature at offset {chunkOffset}: '{magicAscii}'. Expected 'GEOM'.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }
        curr += 4;

        // Version (uint32)
        uint geomVersion = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        if (geomVersion < 1 || geomVersion > 12)
        {
            issues.Add(new ConversionIssue(
                "GEOM002",
                $"Unsupported GEOM chunk format version: {geomVersion}. Supported versions: 1..12.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        // TgiOffset (uint32), TgiSize (uint32), EmbeddedId (uint32)
        if (curr + 12 > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue(
                "GEOM005",
                $"Truncated GEOM header TGI/EmbeddedId fields at chunk offset {curr}.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        uint rawTgiOffset = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint tgiSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        ulong shaderId = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        // Reference geom_write.py lines 62 & 143:
        // rawTgiOffset is written as tailStart - 12 (where 12 is tgiOffsetFieldOffset 8 + 4).
        // Therefore, actualTgiPos = 12 + rawTgiOffset!
        int actualTgiPos = rawTgiOffset > 0 ? (int)(12 + rawTgiOffset) : 0;

        // Validate tgiSize and embedded TGI tail formula: tgiSize == 4 + tgiCount * 16 (GEOM005)
        if (tgiSize > 0)
        {
            try
            {
                checked
                {
                    long tgiEndOffset = (long)actualTgiPos + tgiSize;
                    if (actualTgiPos < 0 || actualTgiPos > chunkSpan.Length || tgiEndOffset > chunkSpan.Length)
                    {
                        issues.Add(new ConversionIssue(
                            "GEOM005",
                            $"Malformed embedded TGI section: offset {actualTgiPos} + size {tgiSize} = {tgiEndOffset} exceeds chunk length {chunkSpan.Length}.",
                            ConversionIssueSeverity.Error
                        ));
                        return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                    }

                    if (tgiSize < 4 || actualTgiPos + 4 > chunkSpan.Length)
                    {
                        issues.Add(new ConversionIssue(
                            "GEOM005",
                            $"Truncated embedded TGI count header at chunk offset {actualTgiPos}.",
                            ConversionIssueSeverity.Error
                        ));
                        return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                    }

                    uint embeddedTgiCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(actualTgiPos, 4));
                    long expectedTgiSize = 4L + (long)embeddedTgiCount * 16;
                    if (tgiSize != expectedTgiSize)
                    {
                        issues.Add(new ConversionIssue(
                            "GEOM005",
                            $"Embedded TGI size mismatch: tgiSize is {tgiSize} bytes, but 4 + tgiCount({embeddedTgiCount}) * 16 is {expectedTgiSize} bytes.",
                            ConversionIssueSeverity.Error
                        ));
                        return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                    }
                }
            }
            catch (OverflowException)
            {
                issues.Add(new ConversionIssue(
                    "GEOM004",
                    "Overflow verifying embedded TGI section bounds.",
                    ConversionIssueSeverity.Error
                ));
                return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
            }
        }

        // Check for Optional MTNF Shader Block or EmbeddedId == 0 Padding (geom_write.py line 70)
        if (curr + 4 <= chunkSpan.Length)
        {
            uint firstWord = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));

            if (curr + 8 <= chunkSpan.Length)
            {
                uint secondWord = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr + 4, 4));

                if (secondWord == MtnfMagicLe) // Pattern A: MtnfSize (4B) + "MTNF" (4B)
                {
                    try
                    {
                        checked
                        {
                            long nextPos = (long)curr + 8 + firstWord;
                            if (nextPos > chunkSpan.Length)
                            {
                                issues.Add(new ConversionIssue(
                                    "GEOM005",
                                    $"Truncated MTNF shader metadata block: size {firstWord} at offset {curr} exceeds chunk length {chunkSpan.Length}.",
                                    ConversionIssueSeverity.Error
                                ));
                                return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                            }
                            curr = (int)nextPos;
                        }
                    }
                    catch (OverflowException)
                    {
                        issues.Add(new ConversionIssue(
                            "GEOM004",
                            "Overflow calculating MTNF shader block size.",
                            ConversionIssueSeverity.Error
                        ));
                        return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                    }
                }
                else if (firstWord == MtnfMagicLe) // Pattern B: "MTNF" (4B) + MtnfSize (4B)
                {
                    try
                    {
                        checked
                        {
                            long nextPos = (long)curr + 8 + secondWord;
                            if (nextPos > chunkSpan.Length)
                            {
                                issues.Add(new ConversionIssue(
                                    "GEOM005",
                                    $"Truncated MTNF shader metadata block: size {secondWord} at offset {curr} exceeds chunk length {chunkSpan.Length}.",
                                    ConversionIssueSeverity.Error
                                ));
                                return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                            }
                            curr = (int)nextPos;
                        }
                    }
                    catch (OverflowException)
                    {
                        issues.Add(new ConversionIssue(
                            "GEOM004",
                            "Overflow calculating MTNF shader block size.",
                            ConversionIssueSeverity.Error
                        ));
                        return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                    }
                }
                else if (firstWord == 0 && shaderId == 0)
                {
                    // EmbeddedId == 0 padding word (geom_write.py line 70): skip 4 bytes
                    curr += 4;
                }
            }
            else if (firstWord == 0 && shaderId == 0)
            {
                curr += 4;
            }
        }

        // MergeGroup (int32), SortOrder (int32), VertexCount (uint32), VertexElementCount (uint32)
        if (curr + 16 > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue(
                "GEOM005",
                $"Truncated GEOM mesh metadata fields at chunk offset {curr}.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        int mergeGroup = BinaryPrimitives.ReadInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        int sortOrder = BinaryPrimitives.ReadInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint vertexCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint vertexElementCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        // Parse 9-Byte Vertex Element Descriptors:
        // Reference geom_write.py layout:
        // DataType: uint32 LE (offset 0..3)
        // Format: uint32 LE (offset 4..7)
        // SizeBytes: byte (offset 8)
        uint vertexStrideBytes = 0;
        if (vertexElementCount > 0 && vertexElementCount <= 32)
        {
            int descriptorsSizeBytes = (int)vertexElementCount * 9;
            if (curr + descriptorsSizeBytes > chunkSpan.Length)
            {
                issues.Add(new ConversionIssue(
                    "GEOM005",
                    $"Truncated 9-byte vertex element descriptors section at chunk offset {curr}.",
                    ConversionIssueSeverity.Error
                ));
                return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
            }

            for (uint i = 0; i < vertexElementCount; i++)
            {
                int descriptorOffset = curr + (int)i * 9;
                uint dataType = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(descriptorOffset, 4));
                uint format = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(descriptorOffset + 4, 4));
                byte elementSizeBytes = chunkSpan[descriptorOffset + 8];

                vertexStrideBytes += elementSizeBytes > 0 ? elementSizeBytes : GetVertexFormatSizeBytes(format);
            }
            curr += descriptorsSizeBytes;
        }

        if (vertexStrideBytes == 0)
        {
            vertexStrideBytes = 36; // Fallback position (12B) + normal (12B) + UV (12B)
        }

        // Skip Vertex Buffer
        try
        {
            checked
            {
                long vertexDataBytes = (long)vertexCount * vertexStrideBytes;
                if (curr + vertexDataBytes > chunkSpan.Length)
                {
                    issues.Add(new ConversionIssue(
                        "GEOM005",
                        $"Truncated GEOM vertex data buffer: expected {vertexDataBytes} bytes at chunk offset {curr}, actual chunk length {chunkSpan.Length}.",
                        ConversionIssueSeverity.Error
                    ));
                    return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                }
                curr += (int)vertexDataBytes;
            }
        }
        catch (OverflowException)
        {
            issues.Add(new ConversionIssue(
                "GEOM004",
                "Overflow calculating vertex data buffer size.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        // Read Face Group Section Strictly Aligned with geom_write.py lines 100-120:
        // groupMarker: uint32 LE (4 bytes)
        // faceFormat: byte (1 byte)
        // facePointCount: uint32 LE (4 bytes)
        // Index Buffer: facePointCount * 2 bytes (16-bit UInt16 indices)
        if (curr + 9 > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue(
                "GEOM005",
                $"Truncated GEOM face group header at chunk offset {curr}.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        uint groupMarker = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        byte faceFormat = chunkSpan[curr];
        curr += 1;

        uint indexCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        if (indexCount % 3 != 0)
        {
            issues.Add(new ConversionIssue(
                "GEOM003",
                $"Invalid GEOM face point count: {indexCount} is not a multiple of 3.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        // Skip Index Buffer (FacePointCount * 2 bytes for 16-bit UInt16 indices)
        try
        {
            checked
            {
                long indexBufferBytes = (long)indexCount * 2;
                if (curr + indexBufferBytes > chunkSpan.Length)
                {
                    issues.Add(new ConversionIssue(
                        "GEOM005",
                        $"Truncated GEOM index buffer: expected {indexBufferBytes} bytes at chunk offset {curr}, actual chunk length {chunkSpan.Length}.",
                        ConversionIssueSeverity.Error
                    ));
                    return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                }
                curr += (int)indexBufferBytes;
            }
        }
        catch (OverflowException)
        {
            issues.Add(new ConversionIssue(
                "GEOM004",
                "Overflow calculating index buffer size.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        // Read Skin Controller & Bone Hash Section with Strict Truncation Guard (GEOM005)
        uint boneCount = 0;
        if (curr + 8 > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue(
                "GEOM005",
                $"Truncated GEOM skin controller header at chunk offset {curr}.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        int skinControllerIndex = BinaryPrimitives.ReadInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        boneCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        // Verify Bone Hashes payload (boneCount * 4 bytes)
        try
        {
            checked
            {
                long boneHashBytes = (long)boneCount * 4;
                if (curr + boneHashBytes > chunkSpan.Length)
                {
                    issues.Add(new ConversionIssue(
                        "GEOM005",
                        $"Truncated GEOM bone hash list: expected {boneHashBytes} bytes at chunk offset {curr}, actual chunk length {chunkSpan.Length}.",
                        ConversionIssueSeverity.Error
                    ));
                    return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
                }
                curr += (int)boneHashBytes;
            }
        }
        catch (OverflowException)
        {
            issues.Add(new ConversionIssue(
                "GEOM004",
                "Overflow calculating bone hash list size.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        // Structural Counts Bounds Checks (GEOM003)
        if (vertexCount == 0 || indexCount == 0 || vertexStrideBytes == 0)
        {
            issues.Add(new ConversionIssue(
                "GEOM003",
                $"Invalid GEOM structural count/stride: VertexCount={vertexCount}, IndexCount={indexCount}, VertexStrideBytes={vertexStrideBytes}.",
                ConversionIssueSeverity.Error
            ));
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues.AsReadOnly());
        }

        uint faceCount = indexCount / 3;

        var metadata = new Ts3GeomMetadata(
            ContainerFormat: containerFormat,
            RcolVersion: rcolVersion,
            GeomVersion: geomVersion,
            VertexCount: vertexCount,
            FaceCount: faceCount,
            IndexCount: indexCount,
            VertexStrideBytes: vertexStrideBytes,
            BoneCount: boneCount,
            ExternalTgiCount: externalTgiCount,
            InternalTgiCount: internalTgiCount,
            ShaderId: shaderId,
            TotalSizeBytes: buffer.Length,
            GeomChunkOffset: chunkOffset,
            GeomChunkSize: chunkSize
        );

        return new Ts3GeomParseResult(
            IsSuccess: true,
            Metadata: metadata,
            Issues: issues.AsReadOnly()
        );
    }

    private static uint GetVertexFormatSizeBytes(uint format) => format switch
    {
        1 => 4,  // Float1
        2 => 8,  // Float2 (UV)
        3 => 12, // Float3 (Position, Normal)
        4 => 16, // Float4 (Tangent)
        5 => 4,  // Byte4 / Color
        6 => 4,  // UByte4 / BoneIndex
        7 => 4,  // Short2
        8 => 8,  // Short4
        _ => 4
    };

    public Ts3GeomParseResult Read(Stream stream)
    {
        if (stream == null || !stream.CanRead)
        {
            var issues = new[]
            {
                new ConversionIssue("GEOM000", "Stream is null or unreadable.", ConversionIssueSeverity.Error)
            };
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues);
        }

        long startPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var result = Read(memoryStream.ToArray());

            if (!result.IsSuccess && stream.CanSeek)
            {
                stream.Position = startPosition;
            }

            return result;
        }
        catch (Exception ex)
        {
            if (stream.CanSeek)
            {
                stream.Position = startPosition;
            }

            var issues = new[]
            {
                new ConversionIssue("GEOM000", $"Error reading GEOM stream: {ex.Message}", ConversionIssueSeverity.Error)
            };
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues);
        }
    }

    public async Task<Ts3GeomParseResult> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead)
        {
            var issues = new[]
            {
                new ConversionIssue("GEOM000", "Stream is null or unreadable.", ConversionIssueSeverity.Error)
            };
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues);
        }

        long startPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            var result = Read(memoryStream.ToArray());

            if (!result.IsSuccess && stream.CanSeek)
            {
                stream.Position = startPosition;
            }

            return result;
        }
        catch (Exception ex)
        {
            if (stream.CanSeek)
            {
                stream.Position = startPosition;
            }

            var issues = new[]
            {
                new ConversionIssue("GEOM000", $"Error reading GEOM stream: {ex.Message}", ConversionIssueSeverity.Error)
            };
            return new Ts3GeomParseResult(IsSuccess: false, Metadata: null, Issues: issues);
        }
    }
}
