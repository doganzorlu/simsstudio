using System;
using System.Collections.Generic;
using System.Linq;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record CanonicalMesh
{
    public string Name { get; }
    public IReadOnlyList<CanonicalVertex> Vertices { get; }
    public IReadOnlyList<CanonicalFace> Faces { get; }
    public IReadOnlyList<CanonicalMaterialSlot> Materials { get; }
    public CanonicalCoordinateSystem CoordinateSystem { get; }
    public GameVersion SourceGameVersion { get; }
    public IReadOnlyList<ConversionIssue> Issues { get; }

    public CanonicalMesh(
        string name,
        IEnumerable<CanonicalVertex>? vertices,
        IEnumerable<CanonicalFace>? faces,
        IEnumerable<CanonicalMaterialSlot>? materials,
        CanonicalCoordinateSystem coordinateSystem,
        GameVersion sourceGameVersion,
        IEnumerable<ConversionIssue>? issues)
    {
        Name = name ?? string.Empty;
        Vertices = vertices != null ? Array.AsReadOnly(vertices.ToArray()) : Array.Empty<CanonicalVertex>();
        Faces = faces != null ? Array.AsReadOnly(faces.ToArray()) : Array.Empty<CanonicalFace>();
        Materials = materials != null ? Array.AsReadOnly(materials.ToArray()) : Array.Empty<CanonicalMaterialSlot>();
        CoordinateSystem = coordinateSystem;
        SourceGameVersion = sourceGameVersion;
        Issues = issues != null ? Array.AsReadOnly(issues.ToArray()) : Array.Empty<ConversionIssue>();
    }
}
