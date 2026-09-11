using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectTs4ResourceGenerator
{
    /// <summary>
    /// Generates TS4 Object Catalog (COBJ), Model (MODL), Model LOD (MLOD), and Material resources
    /// while establishing verified TGI relationships and rejecting unverified references.
    /// </summary>
    Ts4ResourceGenerationResult GenerateResources(DecorativeObjectConversionInputBundle bundle);
}
