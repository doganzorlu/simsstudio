using System.Collections.Generic;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Contracts;

public interface ITs4CasPartPayloadBuilder
{
    byte[] BuildCasPartPayload(
        PackageResourceId caspId,
        PackageResourceId geomId,
        PackageResourceId textureId,
        CasPartMetadata? metadata,
        List<DecorativeObjectSourceResourceLink>? links = null);
}
