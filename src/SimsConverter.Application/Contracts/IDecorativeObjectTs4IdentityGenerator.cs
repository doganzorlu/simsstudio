using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectTs4IdentityGenerator
{
    /// <summary>
    /// Maps a source TS3 PackageResourceId to a target TS4 PackageResourceId deterministically.
    /// </summary>
    PackageResourceId MapResourceIdentity(PackageResourceId sourceId, uint targetTypeId, uint? targetGroupId = null);

    /// <summary>
    /// Generates a deterministic TS4 PackageResourceId based on a seed string and target type ID.
    /// </summary>
    PackageResourceId GenerateDeterministicResourceId(uint targetTypeId, string seedName, uint groupId = 0x00000000);
}
