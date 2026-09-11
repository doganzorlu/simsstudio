namespace SimsConverter.Domain.Enums;

/// <summary>
/// Capability status for converting specific DBPF resource types between TS3 and TS4.
/// </summary>
public enum ConversionCapabilityStatus
{
    /// <summary>
    /// Resource type is fully analyzed and transformed between TS3 and TS4.
    /// </summary>
    Supported = 0,

    /// <summary>
    /// Resource type is pass-through / preserved as neutral metadata without transformation.
    /// </summary>
    PassThrough = 1,

    /// <summary>
    /// Resource type is unsupported for decorative object conversion and will be skipped.
    /// </summary>
    Unsupported = 2
}
