using System;
using SimsConverter.Domain.Models;

namespace SimsConverter.Textures.Contracts;

public interface IDdsPayloadValidator
{
    DdsPayloadValidationResult Validate(DdsTextureMetadata metadata, ulong actualPayloadBytes);
    DdsPayloadValidationResult Validate(DdsTextureMetadata metadata, ReadOnlySpan<byte> fullDdsPayload);
}
