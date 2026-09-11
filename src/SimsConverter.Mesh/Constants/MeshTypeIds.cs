namespace SimsConverter.Mesh.Constants;

public static class MeshTypeIds
{
    /// <summary>
    /// Sims 3 &amp; Sims 4 Geometry Mesh Resource (GEOM).
    /// Verified via S3PE / ModTheSims / Sims 4 Modders Reference Specifications (0x015A1849).
    /// </summary>
    public const uint Ts3Geom = 0x015A1849;

    /// <summary>
    /// Shared Geometry Mesh Resource alias for GEOM (0x015A1849).
    /// </summary>
    public const uint TsSharedGeom = 0x015A1849;

    /// <summary>
    /// Shared Model Resource (MODL). Used in TS3 &amp; TS4 object models.
    /// Verified via Sims 4 Modders Reference (0x01661233 = Model) and ModTheSims TS3 PackedFileTypes.
    /// </summary>
    public const uint TsSharedModel = 0x01661233;

    /// <summary>
    /// Shared Model LOD Resource (MLOD). Used in TS3 &amp; TS4 object models.
    /// Verified via Sims 4 Modders Reference (0x01D10F34 = Model LOD) and ModTheSims TS3 PackedFileTypes.
    /// </summary>
    public const uint TsSharedModelLod = 0x01D10F34;

    /// <summary>
    /// Shared Rig / Skeleton Resource (RIG). Used in TS3 &amp; TS4.
    /// Verified via S3PE / S4Studio Resource Type Index.
    /// </summary>
    public const uint TsSharedRig = 0x8EAF13DE;

    /// <summary>
    /// Shared Slot Layout Resource (RSLT). Used in TS3 &amp; TS4.
    /// Verified via S3PE / S4Studio Resource Type Index.
    /// </summary>
    public const uint TsSharedSlot = 0xD3044521;

    /// <summary>
    /// Shared Blend Geometry / Morph Mesh Resource (BGEO). Used in TS3 &amp; TS4.
    /// Verified via Sims 4 Modders Reference (0x067CAA11 = Blend Geometry) and ModTheSims TS3 PackedFileTypes.
    /// </summary>
    public const uint TsSharedBlendGeometry = 0x067CAA11;
}
