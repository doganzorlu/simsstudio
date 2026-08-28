using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Package.Services;

public class Sims3PackXmlParser : ISims3PackXmlParser
{
    public const int MaxXmlMetadataBytes = 10 * 1024 * 1024; // 10 MB allocation limit

    public async Task<Sims3PackParseResult> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Sims3PackParseResult.Failure("S3PX000", "File path is null, empty, or file does not exist.");
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return await ParseAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            return Sims3PackParseResult.Failure("S3PX009", $"File access error during Sims3Pack XML parse: {ex.Message}");
        }
    }

    public async Task<Sims3PackParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead)
        {
            return Sims3PackParseResult.Failure("S3PX000", "Stream is null or unreadable.");
        }

        try
        {
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            // Step 1: Parse outer binary header
            byte[] headerLenBuffer = new byte[4];
            int read = await ReadExactAsync(stream, headerLenBuffer, cancellationToken);
            if (read < 4)
            {
                return Sims3PackParseResult.Failure("S3PX004", "Truncated Sims3Pack outer header.");
            }

            uint signatureLength = BinaryPrimitives.ReadUInt32LittleEndian(headerLenBuffer);
            if (signatureLength < 7 || signatureLength > 64)
            {
                return Sims3PackParseResult.Failure("S3PX005", $"Invalid TS3Pack signature length: {signatureLength}.");
            }

            byte[] sigBuffer = new byte[signatureLength];
            read = await ReadExactAsync(stream, sigBuffer, cancellationToken);
            if (read < signatureLength)
            {
                return Sims3PackParseResult.Failure("S3PX004", "Truncated TS3Pack signature string.");
            }

            string rawSig = Encoding.ASCII.GetString(sigBuffer);
            // Strict signature check: MUST equal "TS3Pack" (7 bytes) or "TS3Pack\0" (8 bytes). Space padding is rejected.
            if (!rawSig.Equals("TS3Pack", StringComparison.Ordinal) && !rawSig.Equals("TS3Pack\0", StringComparison.Ordinal))
            {
                return Sims3PackParseResult.Failure("S3PX005", $"Invalid TS3Pack signature string: '{rawSig}'.");
            }

            byte[] versionAndXmlLenBuffer = new byte[6];
            read = await ReadExactAsync(stream, versionAndXmlLenBuffer, cancellationToken);
            if (read < 6)
            {
                return Sims3PackParseResult.Failure("S3PX004", "Truncated TS3Pack version/xmlLength header.");
            }

            ushort version = BinaryPrimitives.ReadUInt16LittleEndian(versionAndXmlLenBuffer.AsSpan(0, 2));
            uint xmlLength = BinaryPrimitives.ReadUInt32LittleEndian(versionAndXmlLenBuffer.AsSpan(2, 4));

            if (xmlLength == 0)
            {
                return Sims3PackParseResult.Failure("S3PX002", "XML metadata section length is 0 bytes.");
            }

            // High - Bounded Allocation Guard: Reject xmlLength exceeding 10MB BEFORE memory allocation
            if (xmlLength > MaxXmlMetadataBytes)
            {
                return Sims3PackParseResult.Failure(
                    "S3PX006",
                    $"XML metadata section length ({xmlLength} bytes) exceeds maximum allowed limit of {MaxXmlMetadataBytes} bytes."
                );
            }

            if (stream.CanSeek && (stream.Position + xmlLength > stream.Length))
            {
                return Sims3PackParseResult.Failure("S3PX003", $"XML length ({xmlLength} bytes) extends beyond stream boundary.");
            }

            // Step 2: Read EXACTLY xmlLength bytes safely
            byte[] xmlBuffer = new byte[xmlLength];
            int xmlBytesRead = await ReadExactAsync(stream, xmlBuffer, cancellationToken);

            if (xmlBytesRead < xmlLength)
            {
                return Sims3PackParseResult.Failure("S3PX003", $"Premature end of stream while reading XML metadata section (read {xmlBytesRead}/{xmlLength} bytes).");
            }

            string xmlString = Encoding.UTF8.GetString(xmlBuffer).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (string.IsNullOrWhiteSpace(xmlString))
            {
                return Sims3PackParseResult.Failure("S3PX002", "XML metadata section is empty or whitespace.");
            }

            // Step 3: Secure XML parsing (prohibit DTD & external entities)
            var xmlSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 1024,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };

            using var stringReader = new StringReader(xmlString);
            using var xmlReader = XmlReader.Create(stringReader, xmlSettings);

            XDocument doc = XDocument.Load(xmlReader);
            XElement root = doc.Root ?? new XElement("Sims3Pack");

            string rootElementName = root.Name.LocalName;
            string declaredEncoding = doc.Declaration?.Encoding ?? "utf-8";

            List<string> embeddedFiles = new();
            foreach (var elem in root.Descendants())
            {
                string name = elem.Name.LocalName;
                if (name.Equals("Filename", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Package", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("File", StringComparison.OrdinalIgnoreCase))
                {
                    string val = elem.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !embeddedFiles.Contains(val))
                    {
                        embeddedFiles.Add(val);
                    }
                }
            }

            // Best-effort optional semantic fields
            string? title = FindElementValue(root, "Title", "Name", "AssetTitle", "localizedTitle");
            string? assetId = FindElementValue(root, "AssetId", "GUID", "Id", "id");
            string? assetType = FindElementValue(root, "AssetType", "Type", "Category", "category");
            string? description = FindElementValue(root, "Description", "localizedDescription");

            var metadata = new Sims3PackXmlMetadata(
                RootElementName: rootElementName,
                DeclaredEncoding: declaredEncoding,
                RawXmlSizeBytes: xmlLength,
                EmbeddedFileCount: embeddedFiles.Count,
                EmbeddedFileNames: embeddedFiles.AsReadOnly(),
                Title: title,
                AssetId: assetId,
                AssetType: assetType,
                Description: description,
                Issues: Array.Empty<ConversionIssue>()
            );

            return new Sims3PackParseResult(
                true,
                metadata,
                Array.Empty<ConversionIssue>()
            );
        }
        catch (XmlException ex)
        {
            return Sims3PackParseResult.Failure("S3PX001", $"Malformed XML metadata payload: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Sims3PackParseResult.Failure("S3PX009", $"Sims3Pack XML parsing error: {ex.Message}");
        }
    }

    private static string? FindElementValue(XElement root, params string[] candidateTagNames)
    {
        foreach (string tagName in candidateTagNames)
        {
            foreach (var elem in root.Descendants())
            {
                if (elem.Name.LocalName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                {
                    string val = elem.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        return val;
                    }
                }
            }
        }
        return null;
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }
        return totalRead;
    }
}
