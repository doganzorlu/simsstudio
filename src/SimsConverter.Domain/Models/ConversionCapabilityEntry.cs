using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

/// <summary>
/// Capability entry mapping a specific DBPF resource type to its conversion status and note.
/// </summary>
public record ConversionCapabilityEntry(
    uint TypeId,
    string TypeName,
    ConversionCapabilityStatus CapabilityStatus,
    GameVersion SourceGameVersion,
    GameVersion TargetGameVersion,
    int ResourceCount,
    string Note
);
