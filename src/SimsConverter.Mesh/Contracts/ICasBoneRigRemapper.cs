using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Contracts;

public interface ICasBoneRigRemapper
{
    CanonicalMesh RemapSkeletonBones(CanonicalMesh sourceMesh);
}
