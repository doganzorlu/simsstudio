using System;
using System.Text;
using SimsConverter.Application.Contracts;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Services;

public class DecorativeObjectTs4IdentityGenerator : IDecorativeObjectTs4IdentityGenerator
{
    public PackageResourceId MapResourceIdentity(PackageResourceId sourceId, uint targetTypeId, uint? targetGroupId = null)
    {
        if (sourceId == null) throw new ArgumentNullException(nameof(sourceId));

        uint groupId = targetGroupId ?? sourceId.GroupId;
        return new PackageResourceId(targetTypeId, groupId, sourceId.InstanceId);
    }

    public PackageResourceId GenerateDeterministicResourceId(uint targetTypeId, string seedName, uint groupId = 0x00000000)
    {
        if (string.IsNullOrEmpty(seedName))
        {
            seedName = "DefaultDecorativeObjectSeed";
        }

        ulong instanceId = ComputeFnv64Hash(seedName);
        return new PackageResourceId(targetTypeId, groupId, instanceId);
    }

    private static ulong ComputeFnv64Hash(string text)
    {
        const ulong fnvOffsetBasis = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        ulong hash = fnvOffsetBasis;

        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }
}
