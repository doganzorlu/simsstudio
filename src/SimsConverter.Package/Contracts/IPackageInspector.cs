using System;
using SimsConverter.Package.Models;

namespace SimsConverter.Package.Contracts;

public interface IPackageInspector
{
    PackageHeaderSummary InspectHeader(ReadOnlySpan<byte> headerBuffer);
}
