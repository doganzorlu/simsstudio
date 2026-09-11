using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;

namespace SimsConverter.Application.Services;

public class DecorativeObjectConversionService : IDecorativeObjectConversionService
{
    private readonly IPackageInspectionService _packageInspectionService;
    private readonly IMeshInspectionService _meshInspectionService;
    private readonly ITextureInspectionService _textureInspectionService;
    private readonly IDecorativeObjectSourceGraphBuilder _sourceGraphBuilder;
    private readonly IDecorativeObjectConversionInputBundleBuilder? _inputBundleBuilder;
    private readonly IDecorativeObjectPackageWritePlanBuilder? _writePlanBuilder;
    private readonly IDecorativeObjectPackageWriter? _packageWriter;
    private readonly ITs4ResourcePayloadCompatibilityVerifier _payloadVerifier;
    private readonly IDbpfPackageParser _dbpfParser;
    private readonly IDecorativeObjectConversionCapabilityService _capabilityService;

    public DecorativeObjectConversionService(
        IPackageInspectionService packageInspectionService,
        IMeshInspectionService meshInspectionService,
        ITextureInspectionService textureInspectionService,
        IDecorativeObjectSourceGraphBuilder? sourceGraphBuilder = null,
        IDecorativeObjectConversionInputBundleBuilder? inputBundleBuilder = null,
        IDecorativeObjectPackageWritePlanBuilder? writePlanBuilder = null,
        IDecorativeObjectPackageWriter? packageWriter = null,
        ITs4ResourcePayloadCompatibilityVerifier? payloadVerifier = null,
        IDbpfPackageParser? dbpfParser = null,
        IDecorativeObjectConversionCapabilityService? capabilityService = null)
    {
        _packageInspectionService = packageInspectionService ?? throw new ArgumentNullException(nameof(packageInspectionService));
        _meshInspectionService = meshInspectionService ?? throw new ArgumentNullException(nameof(meshInspectionService));
        _textureInspectionService = textureInspectionService ?? throw new ArgumentNullException(nameof(textureInspectionService));
        _sourceGraphBuilder = sourceGraphBuilder ?? new DecorativeObjectSourceGraphBuilder(packageInspectionService, meshInspectionService, textureInspectionService);
        _inputBundleBuilder = inputBundleBuilder;
        _writePlanBuilder = writePlanBuilder;
        _packageWriter = packageWriter;
        _payloadVerifier = payloadVerifier ?? new Ts4ResourcePayloadCompatibilityVerifier();
        _dbpfParser = dbpfParser ?? new DbpfPackageParser();
        _capabilityService = capabilityService ?? new DecorativeObjectConversionCapabilityService();
    }

    public DecorativeObjectConversionResult CreateConversionPlan(DecorativeObjectConversionRequest request)
    {
        return CreateConversionPlanAsync(request).GetAwaiter().GetResult();
    }

    public async Task<DecorativeObjectConversionResult> CreateConversionPlanAsync(
        DecorativeObjectConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return DecorativeObjectConversionResult.Failure(
                string.Empty,
                string.Empty,
                "CONVA000",
                "Conversion request is null."
            );
        }

        var sourcePath = request.SourcePackagePath;
        var targetPath = request.TargetOutputPath;

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return DecorativeObjectConversionResult.Failure(
                sourcePath ?? string.Empty,
                targetPath ?? string.Empty,
                "CONVA001",
                "Source package file path is null or empty."
            );
        }

        if (!File.Exists(sourcePath))
        {
            return DecorativeObjectConversionResult.Failure(
                sourcePath,
                targetPath ?? string.Empty,
                "CONVA001",
                $"Source package file does not exist: {sourcePath}"
            );
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return DecorativeObjectConversionResult.Failure(
                sourcePath,
                targetPath ?? string.Empty,
                "CONVA002",
                "Target output path is null or empty."
            );
        }

        string fullSourcePath;
        string fullTargetPath;

        try
        {
            fullSourcePath = Path.GetFullPath(sourcePath);
            fullTargetPath = Path.GetFullPath(targetPath);
        }
        catch (Exception ex)
        {
            return DecorativeObjectConversionResult.Failure(
                sourcePath,
                targetPath,
                "CONVA002",
                $"Invalid output or source path format: {ex.Message}"
            );
        }

        if (string.Equals(fullSourcePath, fullTargetPath, StringComparison.OrdinalIgnoreCase))
        {
            return DecorativeObjectConversionResult.Failure(
                sourcePath,
                targetPath,
                "CONVA003",
                "Target output path cannot be identical to source package path."
            );
        }

        var packageResult = await _packageInspectionService.InspectFileAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        if (!packageResult.IsSuccess)
        {
            return DecorativeObjectConversionResult.Failure(
                sourcePath,
                targetPath,
                "CONVA004",
                $"Package inspection failed for source package: {sourcePath}",
                packageResult.Issues
            );
        }

        var sourceGameVersion = request.EffectiveSourceGameVersion;
        var meshResult = _meshInspectionService.InspectPackageMeshes(packageResult, sourceGameVersion);
        var textureResult = _textureInspectionService.InspectPackageTextures(packageResult, sourceGameVersion);
        var sourceGraph = _sourceGraphBuilder.BuildGraph(packageResult, sourceGameVersion);

        var meshCandidates = meshResult.Rows
            .Where(r => r.ClassificationKind == MeshClassificationKind.KnownMesh)
            .ToList();

        var textureCandidates = textureResult.Rows
            .Where(r => r.ClassificationKind == TextureClassificationKind.KnownTexture)
            .ToList();

        int convertableMeshCount = meshCandidates.Count(m => m.CanInspectCanonicalMesh);
        int validTextureCount = textureCandidates.Count(t => t.CanExtractRawPayload);

        bool hasObjectModel = sourceGraph.ObjectModelDecomposition != null || (sourceGraph.OtherResources != null && sourceGraph.OtherResources.Any(r => r.TypeId == 0x01661233 || r.TypeId == 0x01D10F34));

        DecorativeObjectConversionInputBundle? inputBundle = null;
        if (_inputBundleBuilder != null && (sourceGraph.IsSourceGraphReady || hasObjectModel))
        {
            inputBundle = await _inputBundleBuilder.BuildBundleAsync(sourceGraph, targetPath, request.TargetGameVersion, cancellationToken).ConfigureAwait(false);
        }

        DecorativeObjectPackageWritePlan? writePlan = null;
        if (inputBundle != null && inputBundle.IsBundleValid)
        {
            var planBuilder = _writePlanBuilder ?? new DecorativeObjectPackageWritePlanBuilder();
            writePlan = await planBuilder.BuildWritePlanAsync(inputBundle, cancellationToken).ConfigureAwait(false);
        }

        var capabilityMatrix = _capabilityService.EvaluateCapability(packageResult, sourceGameVersion, request.TargetGameVersion);

        var steps = new List<DecorativeObjectConversionStep>
        {
            new DecorativeObjectConversionStep(
                StepId: "STEP-01-VAL-SRC",
                Title: "Source Package Path Validation",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: "Source package path validated successfully.",
                Issues: Array.Empty<ConversionIssue>()
            ),
            new DecorativeObjectConversionStep(
                StepId: "STEP-02-VAL-OUT",
                Title: "Target Output Path Guard Check",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: "Target output path canonical guard check passed.",
                Issues: Array.Empty<ConversionIssue>()
            ),
            new DecorativeObjectConversionStep(
                StepId: "STEP-03-INSPECT-SRC",
                Title: "Source Package Inspection",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: $"Source package inspected. Total resources: {packageResult.Resources?.Count ?? 0}.",
                Issues: packageResult.Issues ?? Array.Empty<ConversionIssue>()
            ),
            new DecorativeObjectConversionStep(
                StepId: "STEP-03B-CAPABILITY-MATRIX",
                Title: "Bidirectional Conversion Capability Matrix Evaluation",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: $"Evaluated capability matrix for {sourceGameVersion} -> {request.TargetGameVersion}. Supported: {capabilityMatrix.SupportedResourceCount}, PassThrough: {capabilityMatrix.PassThroughResourceCount}, Unsupported: {capabilityMatrix.UnsupportedResourceCount}.",
                Issues: capabilityMatrix.Issues
            ),
            new DecorativeObjectConversionStep(
                StepId: "STEP-04-COLLECT-MESH",
                Title: "Mesh Candidates Collection",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: $"Collected {meshCandidates.Count} mesh candidates.",
                Issues: meshResult.Issues ?? Array.Empty<ConversionIssue>()
            ),
            new DecorativeObjectConversionStep(
                StepId: "STEP-05-COLLECT-TEX",
                Title: "Texture Candidates Collection",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: $"Collected {textureCandidates.Count} texture candidates.",
                Issues: textureResult.Issues ?? Array.Empty<ConversionIssue>()
            ),
            new DecorativeObjectConversionStep(
                StepId: "STEP-06-SOURCE-GRAPH",
                Title: "Source Asset Graph Construction",
                Status: sourceGraph.IsSourceGraphReady ? DecorativeObjectConversionStepStatus.Completed : DecorativeObjectConversionStepStatus.Failed,
                Details: $"Built source asset graph. Meshes: {sourceGraph.MeshAssets.Count}, Textures: {sourceGraph.TextureAssets.Count}, Graph Ready: {sourceGraph.IsSourceGraphReady}.",
                Issues: sourceGraph.Issues ?? Array.Empty<ConversionIssue>()
            ),
            new DecorativeObjectConversionStep(
                StepId: "STEP-07-TS4-WRITER",
                Title: "TS4 Target Package Writer Boundary",
                Status: (writePlan != null && writePlan.IsPlanValid) ? DecorativeObjectConversionStepStatus.Completed : DecorativeObjectConversionStepStatus.NotImplemented,
                Details: (writePlan != null && writePlan.IsPlanValid)
                    ? $"TS4 target package write plan constructed with {writePlan.PlannedResources.Count} planned resources."
                    : "NotImplemented: TS4 target package write boundary pending valid input bundle.",
                Issues: writePlan?.Issues ?? Array.Empty<ConversionIssue>()
            )
        };

        var aggregatedIssues = new List<ConversionIssue>();
        if (packageResult.Issues != null) aggregatedIssues.AddRange(packageResult.Issues);
        if (capabilityMatrix.Issues != null) aggregatedIssues.AddRange(capabilityMatrix.Issues);
        if (meshResult.Issues != null) aggregatedIssues.AddRange(meshResult.Issues);
        if (textureResult.Issues != null) aggregatedIssues.AddRange(textureResult.Issues);
        if (sourceGraph.Issues != null) aggregatedIssues.AddRange(sourceGraph.Issues);
        if (inputBundle?.Issues != null) aggregatedIssues.AddRange(inputBundle.Issues);

        var plan = new DecorativeObjectConversionPlan(
            SourcePackagePath: sourcePath,
            TargetOutputPath: targetPath,
            TargetGameVersion: request.TargetGameVersion,
            TotalMeshCandidateCount: meshCandidates.Count,
            TotalTextureCandidateCount: textureCandidates.Count,
            ConvertableMeshCount: convertableMeshCount,
            ValidTextureCount: validTextureCount,
            MeshCandidates: meshCandidates.AsReadOnly(),
            TextureCandidates: textureCandidates.AsReadOnly(),
            Steps: steps.AsReadOnly(),
            IsFeasible: packageResult.IsSuccess && sourceGraph.IsSourceGraphReady,
            SourceGraph: sourceGraph,
            InputBundle: inputBundle,
            CapabilityMatrix: capabilityMatrix
        );

        return new DecorativeObjectConversionResult(
            IsSuccess: packageResult.IsSuccess,
            SourcePackagePath: sourcePath,
            TargetOutputPath: targetPath,
            Plan: plan,
            Issues: aggregatedIssues.AsReadOnly()
        );
    }

    public DecorativeObjectConversionResult ExecuteConversion(DecorativeObjectConversionRequest request)
    {
        return ExecuteConversionAsync(request).GetAwaiter().GetResult();
    }

    public async Task<DecorativeObjectConversionResult> ExecuteConversionAsync(
        DecorativeObjectConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return DecorativeObjectConversionResult.Failure(
                string.Empty,
                string.Empty,
                "CONVE000",
                "Conversion request is null."
            );
        }

        var finalTargetPath = request.TargetOutputPath;
        if (string.IsNullOrWhiteSpace(finalTargetPath))
        {
            return DecorativeObjectConversionResult.Failure(
                request.SourcePackagePath ?? string.Empty,
                string.Empty,
                "CONVA002",
                "Target output path is null or empty."
            );
        }

        var planResult = await CreateConversionPlanAsync(request, cancellationToken).ConfigureAwait(false);
        if (!planResult.IsSuccess || planResult.Plan == null || planResult.Plan.InputBundle == null)
        {
            if (planResult.IsSuccess)
            {
                return DecorativeObjectConversionResult.Failure(
                    request.SourcePackagePath,
                    finalTargetPath,
                    "CONVE000",
                    "Conversion plan construction failed: input bundle is missing or source graph is not ready.",
                    planResult.Issues
                );
            }
            return planResult;
        }

        var inputBundle = planResult.Plan.InputBundle;
        if (!inputBundle.IsBundleValid)
        {
            return DecorativeObjectConversionResult.Failure(
                request.SourcePackagePath,
                finalTargetPath,
                "CONVE001",
                "Conversion input bundle is invalid for package execution.",
                inputBundle.Issues
            );
        }

        var planBuilder = _writePlanBuilder ?? new DecorativeObjectPackageWritePlanBuilder();
        var writePlan = await planBuilder.BuildWritePlanAsync(inputBundle, cancellationToken).ConfigureAwait(false);

        if (!writePlan.IsPlanValid)
        {
            return DecorativeObjectConversionResult.Failure(
                request.SourcePackagePath,
                finalTargetPath,
                "CONVE002",
                "Package write plan is invalid for execution.",
                writePlan.Issues
            );
        }

        // Staging Temp File Strategy: Write to a staging temporary file path first
        string stagingTempPath = finalTargetPath + ".staging." + Guid.NewGuid().ToString("N") + ".package";
        var stagingWritePlan = writePlan with { TargetOutputPath = stagingTempPath };

        void CleanupStagingTempFile()
        {
            if (File.Exists(stagingTempPath))
            {
                try { File.Delete(stagingTempPath); } catch { }
            }
        }

        try
        {
            var writer = _packageWriter ?? new DecorativeObjectPackageWriter();
            var writeResult = await writer.WritePackageAsync(stagingWritePlan, cancellationToken).ConfigureAwait(false);

            if (!writeResult.IsSuccess)
            {
                CleanupStagingTempFile();
                return DecorativeObjectConversionResult.Failure(
                    request.SourcePackagePath,
                    finalTargetPath,
                    "CONVE003",
                    $"Package writing failed: {writeResult.Issues.FirstOrDefault()?.Message}",
                    writeResult.Issues
                );
            }

            // Reopen written staging package & verify payload compatibility + TGI graph integrity
            var parseResult = await _dbpfParser.ParseFileAsync(stagingTempPath, cancellationToken).ConfigureAwait(false);

            if (!parseResult.IsSuccess)
            {
                CleanupStagingTempFile();
                return DecorativeObjectConversionResult.Failure(
                    request.SourcePackagePath,
                    finalTargetPath,
                    "CONVE004",
                    "Written TS4 output package file failed DBPF container parsing.",
                    parseResult.Issues
                );
            }

            var compatResult = await _payloadVerifier.VerifyPackagePayloadsAsync(stagingTempPath, parseResult, cancellationToken).ConfigureAwait(false);

            var steps = new List<DecorativeObjectConversionStep>(planResult.Plan.Steps);

            if (!compatResult.IsSuccess)
            {
                CleanupStagingTempFile();

                steps.Add(new DecorativeObjectConversionStep(
                    StepId: "STEP-08-EXEC-CONV",
                    Title: "TS4 End-to-End Package Execution and Validation",
                    Status: DecorativeObjectConversionStepStatus.Failed,
                    Details: "TS4 output package payload compatibility verification failed.",
                    Issues: compatResult.Issues
                ));

                var updatedPlanFailed = planResult.Plan with { Steps = steps.AsReadOnly() };
                return DecorativeObjectConversionResult.Failure(
                    request.SourcePackagePath,
                    finalTargetPath,
                    "CONVE005",
                    "Written TS4 package payload compatibility verification failed.",
                    compatResult.Issues
                );
            }

            // Commit Strategy: Move fully-verified staging file to final target output path atomically
            File.Move(stagingTempPath, finalTargetPath, overwrite: true);

            steps.Add(new DecorativeObjectConversionStep(
                StepId: "STEP-08-EXEC-CONV",
                Title: "TS4 End-to-End Package Execution and Validation",
                Status: DecorativeObjectConversionStepStatus.Completed,
                Details: $"TS4 package converted & verified successfully with {compatResult.TotalResourcesVerified} verified resources and {compatResult.VerifiedTgiLinkCount} verified TGI graph links.",
                Issues: Array.Empty<ConversionIssue>()
            ));

            var updatedPlanSuccess = planResult.Plan with { Steps = steps.AsReadOnly() };

            return new DecorativeObjectConversionResult(
                IsSuccess: true,
                SourcePackagePath: request.SourcePackagePath,
                TargetOutputPath: finalTargetPath,
                Plan: updatedPlanSuccess,
                Issues: planResult.Issues.Concat(compatResult.Issues).ToList().AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            CleanupStagingTempFile();
            return DecorativeObjectConversionResult.Failure(
                request.SourcePackagePath,
                finalTargetPath,
                "CONVE999",
                $"Unexpected exception during conversion execution: {ex.Message}"
            );
        }
    }
}
