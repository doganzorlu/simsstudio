using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Services;

public static class Ts4GeomPayloadBuilder
{
    public static byte[] BuildGeomPayload(
        PackageResourceId geomId,
        PackageResourceId materialId,
        CanonicalMesh mesh)
    {
        if (geomId == null) throw new ArgumentNullException(nameof(geomId));
        if (materialId == null) throw new ArgumentNullException(nameof(materialId));
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));

        int vertexCount = mesh.Vertices.Count;
        int faceCount = mesh.Faces.Count;

        if (vertexCount == 0 || faceCount == 0)
        {
            throw new ArgumentException("Cannot build TS4 GEOM payload for empty CanonicalMesh with 0 vertices or 0 faces.");
        }

        uint headerSize = 8;
        int geomChunkOffset = 48;
        uint geomVersion = 5;

        byte bytesPerFacePoint = 2;
        int facePointCount = faceCount * 3;
        int boneCount = 1;

        var descriptors = new List<(uint datatype, uint format, byte size)>
        {
            (1, 3, 12), // Position float3 (12B)
            (2, 3, 12), // Normal float3 (12B)
            (3, 2, 8),  // UV0 float2 (8B)
            (4, 4, 4),  // Bone Indices byte4 (4B)
            (5, 2, 4)   // Bone Weights byte4 (4B)
        };

        int elementCount = descriptors.Count;
        int strideBytes = 40; // 12 + 12 + 8 + 4 + 4

        int vertexBufferBytes = vertexCount * strideBytes;
        int indexBufferBytes = facePointCount * bytesPerFacePoint;
        int boneHashBytes = boneCount * 4;
        int submeshBytes = 4 + 1 + 4 + indexBufferBytes;
        int skinControllerOrStitchesBytes = 4;
        int boneSectionBytes = 4 + boneHashBytes;
        int tailTgiBytes = 4 + 16;

        int geomChunkLength = 20 + 16 + (elementCount * 9) + vertexBufferBytes + submeshBytes + skinControllerOrStitchesBytes + boneSectionBytes + tailTgiBytes;
        int tailStartPos = geomChunkLength - tailTgiBytes;
        uint rawTgiOffset = (uint)(tailStartPos - 12);
        uint tgiSize = (uint)tailTgiBytes;

        int totalSizeBytes = geomChunkOffset + geomChunkLength;
        var buffer = new byte[totalSizeBytes];
        var span = buffer.AsSpan();

        // 1. Count-First RCOL Header
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), 1); // ExternalTgiCount = 1
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1); // InternalResourceCount = 1

        // 2. Internal ITG at offset 8 (16B)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8, 8), geomId.InstanceId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), geomId.TypeId); // GEOM TypeId (0x015A1849)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), geomId.GroupId);

        // 3. External TGI at offset 24 (16B)
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), materialId.InstanceId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), materialId.TypeId); // RMAT Material TypeId
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), (uint)geomChunkOffset);

        // 4. Chunk Location Table at offset 40 (8B)
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), (uint)(geomChunkOffset - (int)headerSize));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), (uint)geomChunkLength);

        // 5. GEOM Chunk at offset 48
        var geomSpan = span.Slice(geomChunkOffset);
        Encoding.ASCII.GetBytes("GEOM", geomSpan.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(4, 4), geomVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(8, 4), rawTgiOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(12, 4), tgiSize);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(16, 4), 0); // Shader 0

        int curr = 20;

        // Mesh Header
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 8, 4), (uint)vertexCount);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), (uint)elementCount);
        curr += 16;

        // Element Descriptors
        foreach (var desc in descriptors)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), desc.datatype);
            BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), desc.format);
            geomSpan[curr + 8] = desc.size;
            curr += 9;
        }

        // Vertices from CanonicalMesh
        for (int i = 0; i < vertexCount; i++)
        {
            var v = mesh.Vertices[i];
            float px = v.Position.X;
            float py = v.Position.Y;
            float pz = v.Position.Z;

            float nx = v.Normal?.X ?? 0.0f;
            float ny = v.Normal?.Y ?? 1.0f;
            float nz = v.Normal?.Z ?? 0.0f;

            float u = v.Uv0?.X ?? 0.0f;
            float vCoord = v.Uv0?.Y ?? 0.0f;

            int vOffset = curr + (i * strideBytes);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset, 4), px);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset + 4, 4), py);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset + 8, 4), pz);

            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset + 12, 4), nx);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset + 16, 4), ny);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset + 20, 4), nz);

            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset + 24, 4), u);
            BinaryPrimitives.WriteSingleLittleEndian(geomSpan.Slice(vOffset + 28, 4), vCoord);

            // Bone weights
            byte boneIndex0 = 0;
            ushort boneWeight0 = 255;
            if (v.BoneWeights != null && v.BoneWeights.Count > 0)
            {
                boneIndex0 = (byte)v.BoneWeights[0].BoneIndex;
                boneWeight0 = (ushort)Math.Clamp((int)(v.BoneWeights[0].Weight * 255.0f), 0, 255);
            }

            geomSpan[vOffset + 32] = boneIndex0;
            geomSpan[vOffset + 33] = 0;
            geomSpan[vOffset + 34] = 0;
            geomSpan[vOffset + 35] = 0;

            BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(vOffset + 36, 2), boneWeight0);
            BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(vOffset + 38, 2), 0);
        }

        curr += vertexBufferBytes;

        // Submesh Header & Index Buffer
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        geomSpan[curr + 4] = bytesPerFacePoint;
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 5, 4), (uint)facePointCount);
        curr += 9;

        for (int i = 0; i < faceCount; i++)
        {
            var face = mesh.Faces[i];
            ushort idx0 = (ushort)face.A;
            ushort idx1 = (ushort)face.B;
            ushort idx2 = (ushort)face.C;

            int iOffset = curr + (i * 3 * 2);
            BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(iOffset, 2), idx0);
            BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(iOffset + 2, 2), idx1);
            BinaryPrimitives.WriteUInt16LittleEndian(geomSpan.Slice(iOffset + 4, 2), idx2);
        }

        curr += indexBufferBytes;

        // SkinController / Stitches
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr, 4), 0);
        curr += 4;

        // Bone Section
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), boneCount);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 4, 4), 0x12345678);
        curr += 8;

        // Tail TGI Table
        BinaryPrimitives.WriteInt32LittleEndian(geomSpan.Slice(curr, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(geomSpan.Slice(curr + 4, 8), materialId.InstanceId);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 12, 4), materialId.TypeId);
        BinaryPrimitives.WriteUInt32LittleEndian(geomSpan.Slice(curr + 16, 4), materialId.GroupId);

        return buffer;
    }
}
