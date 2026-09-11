using System;
using System.Collections.Generic;
using System.Reflection;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Domain.Tests;

public class CanonicalMeshValidatorTests
{
    private readonly CanonicalMeshValidator _validator = new();

    private static CanonicalMesh CreateValidTriangleMesh()
    {
        var v0 = new CanonicalVertex(new MeshVector3(0f, 0f, 0f));
        var v1 = new CanonicalVertex(new MeshVector3(1f, 0f, 0f));
        var v2 = new CanonicalVertex(new MeshVector3(0f, 1f, 0f));

        var face = new CanonicalFace(0, 1, 2);

        return new CanonicalMesh(
            name: "TestMesh",
            vertices: new[] { v0, v1, v2 },
            faces: new[] { face },
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );
    }

    [Fact]
    public void Validate_ValidMinimalTriangleMesh_PassesWithNoIssues()
    {
        // Arrange
        var mesh = CreateValidTriangleMesh();

        // Act
        var result = _validator.Validate(mesh);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullMesh_ReturnsControlledFailureMESHV000()
    {
        // Act
        var result = _validator.Validate(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("MESHV000");
    }

    [Fact]
    public void Validate_EmptyVertices_FailsWithMESHV001()
    {
        // Arrange
        var mesh = new CanonicalMesh(
            name: "EmptyVerts",
            vertices: Array.Empty<CanonicalVertex>(),
            faces: new[] { new CanonicalFace(0, 1, 2) },
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _validator.Validate(mesh);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MESHV001");
    }

    [Fact]
    public void Validate_EmptyFaces_FailsWithMESHV002()
    {
        // Arrange
        var v0 = new CanonicalVertex(new MeshVector3(0f, 0f, 0f));
        var mesh = new CanonicalMesh(
            name: "EmptyFaces",
            vertices: new[] { v0 },
            faces: Array.Empty<CanonicalFace>(),
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _validator.Validate(mesh);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MESHV002");
    }

    [Fact]
    public void Validate_OutOfRangeFaceIndex_FailsWithMESHV003()
    {
        // Arrange: 3 vertices (valid indices 0, 1, 2), face references 99
        var baseMesh = CreateValidTriangleMesh();
        var invalidMesh = new CanonicalMesh(
            name: baseMesh.Name,
            vertices: baseMesh.Vertices,
            faces: new[] { new CanonicalFace(0, 1, 99) },
            materials: baseMesh.Materials,
            coordinateSystem: baseMesh.CoordinateSystem,
            sourceGameVersion: baseMesh.SourceGameVersion,
            issues: baseMesh.Issues
        );

        // Act
        var result = _validator.Validate(invalidMesh);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MESHV003");
    }

    [Fact]
    public void Validate_DegenerateTriangle_GeneratesMESHV004Warning()
    {
        // Arrange: Face with duplicate vertex index (0, 1, 1)
        var baseMesh = CreateValidTriangleMesh();
        var degenMesh = new CanonicalMesh(
            name: baseMesh.Name,
            vertices: baseMesh.Vertices,
            faces: new[] { new CanonicalFace(0, 1, 1) },
            materials: baseMesh.Materials,
            coordinateSystem: baseMesh.CoordinateSystem,
            sourceGameVersion: baseMesh.SourceGameVersion,
            issues: baseMesh.Issues
        );

        // Act
        var result = _validator.Validate(degenMesh);

        // Assert
        result.IsSuccess.Should().BeTrue("Warnings alone do not fail mesh validation");
        result.Issues.Should().ContainSingle(i => i.Code == "MESHV004" && i.Severity == ConversionIssueSeverity.Warning);
    }

    [Fact]
    public void Validate_NaNOrInfinityPosition_FailsWithMESHV005()
    {
        // Arrange: Vertex with NaN coordinate
        var nanVert = new CanonicalVertex(new MeshVector3(float.NaN, 0f, 0f));
        var v1 = new CanonicalVertex(new MeshVector3(1f, 0f, 0f));
        var v2 = new CanonicalVertex(new MeshVector3(0f, 1f, 0f));

        var mesh = new CanonicalMesh(
            name: "NaNMesh",
            vertices: new[] { nanVert, v1, v2 },
            faces: new[] { new CanonicalFace(0, 1, 2) },
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _validator.Validate(mesh);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "MESHV005");
    }

    [Fact]
    public void Validate_BoneWeightSumOutsideTolerance_GeneratesMESHV006Warning()
    {
        // Arrange: Vertex with bone weights summing to 0.5f (outside [0.99, 1.01])
        var bwVert = new CanonicalVertex(
            position: new MeshVector3(0f, 0f, 0f),
            boneWeights: new[] { new CanonicalBoneWeight(0, 0.5f) }
        );
        var v1 = new CanonicalVertex(new MeshVector3(1f, 0f, 0f));
        var v2 = new CanonicalVertex(new MeshVector3(0f, 1f, 0f));

        var mesh = new CanonicalMesh(
            name: "BoneWeightMesh",
            vertices: new[] { bwVert, v1, v2 },
            faces: new[] { new CanonicalFace(0, 1, 2) },
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _validator.Validate(mesh);

        // Assert
        result.IsSuccess.Should().BeTrue("Warnings alone do not fail mesh validation");
        result.Issues.Should().ContainSingle(i => i.Code == "MESHV006" && i.Severity == ConversionIssueSeverity.Warning);
    }

    [Fact]
    public void Validate_DeterministicIssueOrdering_PreservesOrderOfErrorsAndWarnings()
    {
        // Arrange: Multiple issues (NaN vert 0, out of range face 0)
        var nanVert = new CanonicalVertex(new MeshVector3(float.NaN, 0f, 0f));
        var v1 = new CanonicalVertex(new MeshVector3(1f, 0f, 0f));

        var mesh = new CanonicalMesh(
            name: "MultiIssueMesh",
            vertices: new[] { nanVert, v1 },
            faces: new[] { new CanonicalFace(0, 1, 99) },
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _validator.Validate(mesh);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().HaveCount(2);
        result.Issues[0].Code.Should().Be("MESHV005");
        result.Issues[1].Code.Should().Be("MESHV003");
    }

    [Fact]
    public void CanonicalMesh_Vertices_ExternalListMutation_DoesNotMutateMeshVertices()
    {
        // Arrange
        var v0 = new CanonicalVertex(new MeshVector3(0f, 0f, 0f));
        var vertList = new List<CanonicalVertex> { v0 };

        var mesh = new CanonicalMesh(
            name: "ImmutabilityTest",
            vertices: vertList,
            faces: Array.Empty<CanonicalFace>(),
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act: Mutate caller list
        vertList.Add(new CanonicalVertex(new MeshVector3(1f, 1f, 1f)));
        vertList.Clear();

        // Assert: Mesh.Vertices remains immutable snapshot
        mesh.Vertices.Should().HaveCount(1);
        mesh.Vertices[0].Position.X.Should().Be(0f);
    }

    [Fact]
    public void CanonicalMesh_Faces_ExternalListMutation_DoesNotMutateMeshFaces()
    {
        // Arrange
        var faceList = new List<CanonicalFace> { new CanonicalFace(0, 1, 2) };

        var mesh = new CanonicalMesh(
            name: "ImmutabilityTest",
            vertices: Array.Empty<CanonicalVertex>(),
            faces: faceList,
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act: Mutate caller list
        faceList.Add(new CanonicalFace(3, 4, 5));

        // Assert
        mesh.Faces.Should().HaveCount(1);
    }

    [Fact]
    public void CanonicalMesh_Materials_ExternalListMutation_DoesNotMutateMeshMaterials()
    {
        // Arrange
        var matList = new List<CanonicalMaterialSlot> { new CanonicalMaterialSlot(0, "MatA") };

        var mesh = new CanonicalMesh(
            name: "ImmutabilityTest",
            vertices: Array.Empty<CanonicalVertex>(),
            faces: Array.Empty<CanonicalFace>(),
            materials: matList,
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: Array.Empty<ConversionIssue>()
        );

        // Act: Mutate caller list
        matList.Add(new CanonicalMaterialSlot(1, "MatB"));

        // Assert
        mesh.Materials.Should().HaveCount(1);
    }

    [Fact]
    public void CanonicalMesh_Issues_ExternalListMutation_DoesNotMutateMeshIssues()
    {
        // Arrange
        var issueList = new List<ConversionIssue> { new ConversionIssue("CODE1", "Message", ConversionIssueSeverity.Warning) };

        var mesh = new CanonicalMesh(
            name: "ImmutabilityTest",
            vertices: Array.Empty<CanonicalVertex>(),
            faces: Array.Empty<CanonicalFace>(),
            materials: Array.Empty<CanonicalMaterialSlot>(),
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims4,
            issues: issueList
        );

        // Act: Mutate caller list
        issueList.Add(new ConversionIssue("CODE2", "Message", ConversionIssueSeverity.Error));

        // Assert
        mesh.Issues.Should().HaveCount(1);
    }

    [Fact]
    public void CanonicalVertex_BoneWeights_ExternalListMutation_DoesNotMutateVertexBoneWeights()
    {
        // Arrange
        var bwList = new List<CanonicalBoneWeight> { new CanonicalBoneWeight(0, 1.0f) };
        var vert = new CanonicalVertex(
            position: new MeshVector3(0f, 0f, 0f),
            boneWeights: bwList
        );

        // Act: Mutate caller list
        bwList.Add(new CanonicalBoneWeight(1, 0.5f));
        bwList.Clear();

        // Assert: Vertex.BoneWeights remains immutable snapshot
        vert.BoneWeights.Should().NotBeNull();
        vert.BoneWeights.Should().HaveCount(1);
        vert.BoneWeights![0].BoneIndex.Should().Be(0);
    }

    [Fact]
    public void CanonicalMesh_CollectionProperties_HaveNoInitOrSetAccessors_PreventingInitBypass()
    {
        // Arrange & Assert via Reflection
        var meshType = typeof(CanonicalMesh);
        var collectionPropNames = new[] { nameof(CanonicalMesh.Vertices), nameof(CanonicalMesh.Faces), nameof(CanonicalMesh.Materials), nameof(CanonicalMesh.Issues) };

        foreach (var propName in collectionPropNames)
        {
            var prop = meshType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            prop.Should().NotBeNull();
            prop!.CanWrite.Should().BeFalse($"Property {propName} MUST be get-only to prevent init-setter bypass");
            prop.SetMethod.Should().BeNull($"Property {propName} MUST NOT have a set or init accessor");
        }
    }

    [Fact]
    public void CanonicalVertex_BoneWeightsProperty_HasNoInitOrSetAccessor_PreventingInitBypass()
    {
        // Arrange & Assert via Reflection
        var vertType = typeof(CanonicalVertex);
        var prop = vertType.GetProperty(nameof(CanonicalVertex.BoneWeights), BindingFlags.Public | BindingFlags.Instance);

        prop.Should().NotBeNull();
        prop!.CanWrite.Should().BeFalse("Property BoneWeights MUST be get-only to prevent init-setter bypass");
        prop.SetMethod.Should().BeNull("Property BoneWeights MUST NOT have a set or init accessor");
    }
}
