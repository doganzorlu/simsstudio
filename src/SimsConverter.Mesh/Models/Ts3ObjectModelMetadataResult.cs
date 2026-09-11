using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Models;

public record Ts3ObjectModelMetadataResult(
    bool IsSuccess,
    PackageResourceId ResourceId,
    Ts3ObjectModelKind ModelKind,
    uint Version,
    uint LodCount,
    IReadOnlyList<Ts3ObjectModelLodInfo> LodInfos,
    IReadOnlyList<Ts3ObjectModelGeometryReference> GeometryReferences,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static Ts3ObjectModelMetadataResult Failure(
        PackageResourceId resourceId,
        Ts3ObjectModelKind modelKind,
        string issueCode,
        string issueMessage)
    {
        return new Ts3ObjectModelMetadataResult(
            IsSuccess: false,
            ResourceId: resourceId,
            ModelKind: modelKind,
            Version: 0,
            LodCount: 0,
            LodInfos: Array.Empty<Ts3ObjectModelLodInfo>(),
            GeometryReferences: Array.Empty<Ts3ObjectModelGeometryReference>(),
            Issues: new[] { new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error) }
        );
    }
}
