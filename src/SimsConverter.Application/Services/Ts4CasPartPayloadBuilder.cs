using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Services;

public class Ts4CasPartPayloadBuilder : ITs4CasPartPayloadBuilder
{
    public byte[] BuildCasPartPayload(
        PackageResourceId caspId,
        PackageResourceId geomId,
        PackageResourceId textureId,
        CasPartMetadata? metadata,
        List<DecorativeObjectSourceResourceLink>? links = null)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header magic: 'CASP'
        writer.Write(Encoding.ASCII.GetBytes("CASP"));
        writer.Write((uint)1); // Version 1

        uint ageGender = metadata?.AgeGenderFlags ?? 0x00000030;
        uint category = metadata?.CategoryFlags ?? 0x00000001;
        uint bodyType = metadata?.BodyType ?? 0x00000003;

        writer.Write(ageGender);
        writer.Write(category);
        writer.Write(bodyType);

        // Reference to GEOM
        writer.Write(geomId.TypeId);
        writer.Write(geomId.GroupId);
        writer.Write(geomId.InstanceId);
        if (links != null)
        {
            links.Add(new DecorativeObjectSourceResourceLink(caspId.FormattedKey, geomId.FormattedKey, "CASP_To_GEOM"));
        }

        // Reference to Texture
        writer.Write(textureId.TypeId);
        writer.Write(textureId.GroupId);
        writer.Write(textureId.InstanceId);
        if (links != null)
        {
            links.Add(new DecorativeObjectSourceResourceLink(caspId.FormattedKey, textureId.FormattedKey, "CASP_To_Texture"));
        }

        return ms.ToArray();
    }
}
