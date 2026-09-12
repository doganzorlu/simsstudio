using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Package.Services;

namespace SimsConverter.Application.Services;

public class Ts3CasPartReader : ITs3CasPartReader
{
    private readonly IPackageResourcePayloadReader _payloadReader;

    public Ts3CasPartReader(IPackageResourcePayloadReader? payloadReader = null)
    {
        _payloadReader = payloadReader ?? new PackageResourcePayloadReader();
    }

    public CasPartMetadata ReadCasPartMetadata(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "CasPart")
    {
        if (string.IsNullOrEmpty(packagePath) || packageResources == null || packageResources.Count == 0)
        {
            return CasPartMetadata.CreateDefault(seedIdentity);
        }

        var caspRow = packageResources.FirstOrDefault(r => r.TypeId == Ts4ResourceTypeIds.CasPartTS3 || r.TypeId == Ts4ResourceTypeIds.CasPartTS4);
        if (caspRow == null)
        {
            return CasPartMetadata.CreateDefault(seedIdentity);
        }

        var entry = caspRow.ToEntry();
        var pRes = _payloadReader.ReadPayload(packagePath, entry);
        if (!pRes.IsSuccess || pRes.Payload == null || pRes.Payload.Length < 16)
        {
            return CasPartMetadata.CreateDefault(seedIdentity);
        }

        byte[] payload = pRes.Payload;
        return ParseCaspPayload(payload, caspRow, seedIdentity);
    }

    public async Task<CasPartMetadata> ReadCasPartMetadataAsync(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "CasPart",
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ReadCasPartMetadata(packagePath, packageResources, seedIdentity), cancellationToken).ConfigureAwait(false);
    }

    private static CasPartMetadata ParseCaspPayload(byte[] payload, PackageResourceRow caspRow, string seedIdentity)
    {
        try
        {
            uint ageGender = 0x00000030; // Adult Female default
            uint category = 0x00000001;  // Everyday
            uint bodyType = 0x00000003;  // Outfit/Body

            if (payload.Length >= 16)
            {
                uint version = BitConverter.ToUInt32(payload, 0);
                if (payload.Length >= 20)
                {
                    ageGender = BitConverter.ToUInt32(payload, 4);
                    category = BitConverter.ToUInt32(payload, 8);
                    bodyType = BitConverter.ToUInt32(payload, 12);
                }
            }

            string identityTitle = !string.IsNullOrWhiteSpace(seedIdentity) && seedIdentity != "CasPart"
                ? seedIdentity
                : $"CASP_{caspRow.InstanceHex}";

            return new CasPartMetadata(
                AgeGenderFlags: ageGender != 0 ? ageGender : 0x00000030,
                CategoryFlags: category != 0 ? category : 0x00000001,
                BodyType: bodyType,
                PartTitle: identityTitle,
                PartIdentityKey: caspRow.FormattedKey,
                SwatchColors: Array.Empty<uint>(),
                IsDefaultFallback: false
            );
        }
        catch
        {
            return CasPartMetadata.CreateDefault(seedIdentity);
        }
    }
}
