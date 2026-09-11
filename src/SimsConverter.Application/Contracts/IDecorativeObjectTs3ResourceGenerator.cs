using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectTs3ResourceGenerator
{
    /// <summary>
    /// Generates TS3 Model (MODL), Model LOD (MLOD), RIG, RSLT, and GEOM target resources
    /// while establishing verified resource identity mapping for reverse conversion (TS4 -> TS3).
    /// </summary>
    Ts3ResourceGenerationResult GenerateResources(DecorativeObjectConversionInputBundle bundle);
}
