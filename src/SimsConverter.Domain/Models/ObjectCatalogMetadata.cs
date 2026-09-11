namespace SimsConverter.Domain.Models;

/// <summary>
/// Represents object catalog identity and metadata transferred between TS3 and TS4 packages.
/// Includes price, category/catalog group, placement flags, footprint hash/reference, title/identity, and fallback tracking.
/// </summary>
public record ObjectCatalogMetadata(
    uint Price,
    uint CatalogGroup,
    uint PlacementFlags,
    uint FootprintHash,
    string ObjectTitle,
    string ObjectIdentityKey,
    bool IsDefaultFallback = false,
    string? FootprintReferenceKey = null
)
{
    public static ObjectCatalogMetadata CreateDefault(string seedIdentity = "DecorativeObject") =>
        new ObjectCatalogMetadata(
            Price: 100,
            CatalogGroup: 0,
            PlacementFlags: 0x00000001, // Default floor/surface placement
            FootprintHash: 0x00000000,
            ObjectTitle: seedIdentity,
            ObjectIdentityKey: seedIdentity,
            IsDefaultFallback: true
        );
}
