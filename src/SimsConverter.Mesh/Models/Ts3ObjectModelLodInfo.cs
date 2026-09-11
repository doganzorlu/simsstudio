using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Models;

public record Ts3ObjectModelLodInfo(
    uint LodIndex,
    uint GroupCount,
    PackageResourceId? AssociatedResourceId
);
