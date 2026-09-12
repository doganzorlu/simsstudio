using System.Collections.Generic;
using System.Linq;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;

namespace SimsConverter.Mesh.Services;

public class CasBoneRigRemapper : ICasBoneRigRemapper
{
    private static readonly Dictionary<uint, uint> Ts3ToTs4BoneMap = new()
    {
        { 0x5C808E5Cu, 0x00000000u }, // Pelvis / Root
        { 0x93DEBC97u, 0x00000001u }, // Spine0
        { 0x93DEBC96u, 0x00000002u }, // Spine1
        { 0x93DEBC95u, 0x00000003u }, // Spine2 / Chest
        { 0x1A2B3C4Du, 0x00000004u }, // Neck
        { 0x2A3B4C5Eu, 0x00000005u }, // Head
        { 0x3A4B5C6Fu, 0x00000006u }, // L_Clavicle
        { 0x4A5B6C70u, 0x00000007u }, // L_UpperArm
        { 0x5A6B7C81u, 0x00000008u }, // L_Forearm
        { 0x6A7B8C92u, 0x00000009u }, // L_Hand
        { 0x7A8B9CA3u, 0x0000000Au }, // R_Clavicle
        { 0x8A9BACB4u, 0x0000000Bu }, // R_UpperArm
        { 0x9AACBDC5u, 0x0000000Cu }, // R_Forearm
        { 0xABBCCDD6u, 0x0000000Du }, // R_Hand
        { 0xBCCDDEE7u, 0x0000000Eu }, // L_Thigh
        { 0xCDDEEFF8u, 0x0000000Fu }, // L_Calf
        { 0xDEEFF009u, 0x00000010u }, // L_Foot
        { 0xEFF0011Au, 0x00000011u }, // R_Thigh
        { 0xF001122Bu, 0x00000012u }, // R_Calf
        { 0x0112233Cu, 0x00000013u }  // R_Foot
    };

    public CanonicalMesh RemapSkeletonBones(CanonicalMesh sourceMesh)
    {
        if (sourceMesh == null || sourceMesh.Vertices == null || sourceMesh.Vertices.Count == 0)
        {
            return sourceMesh!;
        }

        var remappedVertices = new List<CanonicalVertex>(sourceMesh.Vertices.Count);
        foreach (var v in sourceMesh.Vertices)
        {
            if (v == null)
            {
                remappedVertices.Add(v!);
                continue;
            }

            if (v.BoneWeights == null || v.BoneWeights.Count == 0)
            {
                remappedVertices.Add(v);
                continue;
            }

            var remappedWeights = new List<CanonicalBoneWeight>(v.BoneWeights.Count);
            foreach (var bw in v.BoneWeights)
            {
                uint srcHash = (uint)bw.BoneIndex;
                int targetBoneIndex = Ts3ToTs4BoneMap.TryGetValue(srcHash, out uint mappedId) ? (int)mappedId : bw.BoneIndex;
                remappedWeights.Add(new CanonicalBoneWeight(targetBoneIndex, bw.Weight));
            }

            remappedVertices.Add(new CanonicalVertex(v.Position, v.Normal, v.Tangent, v.Uv0, v.Uv1, remappedWeights));
        }

        return new CanonicalMesh(
            sourceMesh.Name,
            remappedVertices,
            sourceMesh.Faces,
            sourceMesh.Materials,
            sourceMesh.CoordinateSystem,
            sourceMesh.SourceGameVersion,
            sourceMesh.Issues
        );
    }
}
