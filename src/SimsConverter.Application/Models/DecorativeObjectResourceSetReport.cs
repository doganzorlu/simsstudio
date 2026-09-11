using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectResourceSetReport(
    string SourcePackagePath,
    string TargetOutputPath,
    int TotalResourceCount,
    int MeshCount,
    int TextureCount,
    int RigCount,
    int RsltCount,
    int OtherCount,
    long TotalPayloadBytes,
    int VerifiedLinkCount,
    IReadOnlyList<DecorativeObjectPackageWriteResourceEntry> AssembledResources,
    IReadOnlyList<ConversionIssue> Issues
);
