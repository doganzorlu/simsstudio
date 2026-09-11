using System;
using System.Collections.Generic;
using System.Linq;

namespace SimsConverter.Domain.Models;

public record CanonicalVertex
{
    public MeshVector3 Position { get; }
    public MeshVector3? Normal { get; }
    public MeshVector4? Tangent { get; }
    public MeshVector2? Uv0 { get; }
    public MeshVector2? Uv1 { get; }
    public IReadOnlyList<CanonicalBoneWeight>? BoneWeights { get; }

    public CanonicalVertex(
        MeshVector3 position,
        MeshVector3? normal = null,
        MeshVector4? tangent = null,
        MeshVector2? uv0 = null,
        MeshVector2? uv1 = null,
        IEnumerable<CanonicalBoneWeight>? boneWeights = null)
    {
        Position = position;
        Normal = normal;
        Tangent = tangent;
        Uv0 = uv0;
        Uv1 = uv1;
        BoneWeights = boneWeights != null ? Array.AsReadOnly(boneWeights.ToArray()) : null;
    }
}
