using System;
using System.Collections.Generic;

namespace SimsConverter.Domain.Models;

/// <summary>
/// Represents Create-a-Sim (CAS) Part catalog identity and metadata transferred between TS3 and TS4 packages.
/// Includes age/gender flags, outfit category flags, body type, color swatches, and resource TGI links.
/// </summary>
public record CasPartMetadata(
    uint AgeGenderFlags,
    uint CategoryFlags,
    uint BodyType,
    string PartTitle,
    string PartIdentityKey,
    IReadOnlyList<uint>? SwatchColors = null,
    bool IsDefaultFallback = false
)
{
    public static CasPartMetadata CreateDefault(string seedIdentity = "CasPart") =>
        new CasPartMetadata(
            AgeGenderFlags: 0x00000030, // Adult Female default
            CategoryFlags: 0x00000001,  // Everyday outfit default
            BodyType: 0x00000003,       // Body / Outfit
            PartTitle: seedIdentity,
            PartIdentityKey: seedIdentity,
            SwatchColors: Array.Empty<uint>(),
            IsDefaultFallback: true
        );
}
