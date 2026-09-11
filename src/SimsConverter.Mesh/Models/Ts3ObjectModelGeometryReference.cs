using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Models;

public record Ts3ObjectModelGeometryReference(
    PackageResourceId TargetResourceId,
    uint GroupIndex,
    string ReferenceType
);
