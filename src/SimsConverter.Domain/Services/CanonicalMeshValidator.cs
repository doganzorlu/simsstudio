using System;
using System.Collections.Generic;
using System.Linq;
using SimsConverter.Domain.Contracts;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Domain.Services;

public class CanonicalMeshValidator : ICanonicalMeshValidator
{
    private const float BoneWeightTolerance = 0.01f;

    public CanonicalMeshValidationResult Validate(CanonicalMesh mesh)
    {
        var issues = new List<ConversionIssue>();

        if (mesh == null)
        {
            issues.Add(new ConversionIssue(
                "MESHV000",
                "CanonicalMesh is null.",
                ConversionIssueSeverity.Error
            ));
            return new CanonicalMeshValidationResult(IsSuccess: false, Issues: issues.AsReadOnly());
        }

        bool hasVertices = mesh.Vertices != null && mesh.Vertices.Count > 0;
        if (!hasVertices)
        {
            issues.Add(new ConversionIssue(
                "MESHV001",
                "CanonicalMesh contains no vertices.",
                ConversionIssueSeverity.Error
            ));
        }

        bool hasFaces = mesh.Faces != null && mesh.Faces.Count > 0;
        if (!hasFaces)
        {
            issues.Add(new ConversionIssue(
                "MESHV002",
                "CanonicalMesh contains no faces.",
                ConversionIssueSeverity.Error
            ));
        }

        int vertexCount = mesh.Vertices?.Count ?? 0;

        // Vertex Validation
        if (hasVertices)
        {
            for (int i = 0; i < mesh.Vertices!.Count; i++)
            {
                var v = mesh.Vertices[i];
                if (v == null)
                {
                    issues.Add(new ConversionIssue(
                        "MESHV005",
                        $"Vertex at index {i} is null.",
                        ConversionIssueSeverity.Error
                    ));
                    continue;
                }

                // Check Position for NaN / Infinity
                if (float.IsNaN(v.Position.X) || float.IsNaN(v.Position.Y) || float.IsNaN(v.Position.Z) ||
                    float.IsInfinity(v.Position.X) || float.IsInfinity(v.Position.Y) || float.IsInfinity(v.Position.Z))
                {
                    issues.Add(new ConversionIssue(
                        "MESHV005",
                        $"Invalid vertex position at index {i}: ({v.Position.X}, {v.Position.Y}, {v.Position.Z}).",
                        ConversionIssueSeverity.Error
                    ));
                }

                // Check Bone Weights
                if (v.BoneWeights != null && v.BoneWeights.Count > 0)
                {
                    float sum = 0f;
                    foreach (var bw in v.BoneWeights)
                    {
                        if (bw.BoneIndex < 0)
                        {
                            issues.Add(new ConversionIssue(
                                "MESHV006",
                                $"Negative bone index {bw.BoneIndex} at vertex {i}.",
                                ConversionIssueSeverity.Warning
                            ));
                        }
                        sum += bw.Weight;
                    }

                    if (Math.Abs(sum - 1.0f) > BoneWeightTolerance)
                    {
                        issues.Add(new ConversionIssue(
                            "MESHV006",
                            $"Bone weight sum at vertex {i} is {sum:F4}, outside tolerance [0.99, 1.01].",
                            ConversionIssueSeverity.Warning
                        ));
                    }
                }
            }
        }

        // Face Validation
        if (hasFaces)
        {
            for (int i = 0; i < mesh.Faces!.Count; i++)
            {
                var f = mesh.Faces[i];

                // Index Out of Range Check
                if (f.A < 0 || f.A >= vertexCount ||
                    f.B < 0 || f.B >= vertexCount ||
                    f.C < 0 || f.C >= vertexCount)
                {
                    issues.Add(new ConversionIssue(
                        "MESHV003",
                        $"Face index out of range at face {i}: triangle ({f.A}, {f.B}, {f.C}) referencing vertex count {vertexCount}.",
                        ConversionIssueSeverity.Error
                    ));
                }

                // Degenerate Triangle Check
                if (f.A == f.B || f.B == f.C || f.A == f.C)
                {
                    issues.Add(new ConversionIssue(
                        "MESHV004",
                        $"Degenerate triangle detected at face {i}: ({f.A}, {f.B}, {f.C}).",
                        ConversionIssueSeverity.Warning
                    ));
                }
            }
        }

        bool isSuccess = !issues.Any(issue => issue.Severity == ConversionIssueSeverity.Error || issue.Severity == ConversionIssueSeverity.Fatal);

        return new CanonicalMeshValidationResult(
            IsSuccess: isSuccess,
            Issues: issues.AsReadOnly()
        );
    }
}
