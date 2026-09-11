using SimsConverter.Domain.Models;

namespace SimsConverter.Domain.Contracts;

public interface ICanonicalMeshValidator
{
    CanonicalMeshValidationResult Validate(CanonicalMesh mesh);
}
