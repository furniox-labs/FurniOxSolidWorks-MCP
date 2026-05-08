using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

/// <summary>
/// Handles part analysis operations.
/// Extracted from AnalysisOperations to reduce file size.
/// </summary>
public class PartAnalysisOperations : OperationHandlerBase
{
    public PartAnalysisOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<PartAnalysisOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override async Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Analysis.AnalyzePart" => await AnalyzePartAsync(parameters, cancellationToken),
            "Analysis.GetMassProperties" => await GetMassPropertiesAsync(parameters, cancellationToken),
            _ => ExecutionResult.Failure($"Unknown part analysis operation: {operation}")
        };
    }

    /// <summary>
    /// Comprehensive part analysis - returns complete part information in one call.
    /// Context-aware: If a part component is selected in an assembly, analyzes that part.
    /// </summary>
    private Task<ExecutionResult> AnalyzePartAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        var includeComponentFolders = GetBoolParam(parameters, "IncludeComponentFolders", false);
        var openReferencedDocs = GetBoolParam(parameters, "OpenReferencedDocs", true);
        ModelDoc2? openedDoc = null;
        bool openedByUs = false;
        ModelDoc2? openedFolderDoc = null;
        bool openedFolderDocByUs = false;

        try
        {
            // ============================================================================
            // STEP 1: Context-aware target detection
            // If a part component is selected in an assembly, analyze that part instead
            // ============================================================================
            ModelDoc2 targetModel = model;
            PartDoc? targetPart = model as PartDoc;
            string? analyzedFromSelection = null;
            Component2? sourceComponent = null; // Store for transform extraction

            // Check if active doc is an assembly with a selected part component
            if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                var selMgr = (ISelectionMgr?)model.SelectionManager;
                if (selMgr != null && selMgr.GetSelectedObjectCount2(-1) > 0)
                {
                    var selectedObj = selMgr.GetSelectedObject6(1, -1);
                    if (selectedObj is IComponent2 selectedComp)
                    {
                        var compModel = selectedComp.GetModelDoc2() as ModelDoc2;
                        if (compModel == null && openReferencedDocs)
                        {
                            var filePath = selectedComp.GetPathName();
                            compModel = AnalysisHelpers.TryOpenModelIfNeeded(app, filePath, swDocumentTypes_e.swDocPART, out openedByUs);
                            openedDoc = compModel;
                        }

                        if (compModel != null && compModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                        {
                            targetModel = compModel;
                            targetPart = (PartDoc)compModel;
                            analyzedFromSelection = selectedComp.Name2;
                            sourceComponent = (Component2)selectedComp; // Save for transform extraction
                            _logger.LogInformation("Analyzing selected part component: {Name}", analyzedFromSelection);
                        }
                    }
                }

                // If no part selected and active is assembly, fail
                if (targetPart == null)
                {
                    return Task.FromResult(ExecutionResult.Failure(
                        "Active document is an assembly. Select a part component to analyze, or open a part document."));
                }
            }
            else if (targetPart == null)
            {
                return Task.FromResult(ExecutionResult.Failure("Active document is not a part"));
            }

            // ============================================================================
            // STEP 2: Get parameters and handle 'fields' mode
            // ============================================================================
            var fields = GetStringParam(parameters, "Fields", "standard").ToLowerInvariant();

            var includeFeatures = GetBoolParam(parameters, "IncludeFeatures", true);
            var includeMassProperties = GetBoolParam(parameters, "IncludeMassProperties", true);
            var includeBodies = GetBoolParam(parameters, "IncludeBodies", true);
            var includeCustomProperties = GetBoolParam(parameters, "IncludeCustomProperties", true);

            // ============================================================================
            // STEP 3: Extract data from target part
            // ============================================================================
            var result = AnalyzePartInternal(
                targetModel,
                targetPart,
                fields,
                includeFeatures,
                includeMassProperties,
                includeBodies,
                includeCustomProperties);

            // 6. Extract Assembly Context (if part is edited in assembly context)
            // Only if analyzing active document directly (not from selection)
            if (analyzedFromSelection == null)
            {
                var assemblyContext = PartAnalysisContextSupport.ExtractPartAssemblyContext(app, targetModel, _logger);
                if (assemblyContext != null)
                {
                    result = result with { AssemblyContext = assemblyContext };
                }
            }

            // 7. Extract Transform (if analyzing from assembly selection)
            if (sourceComponent != null)
            {
                var transform = AnalysisHelpers.ExtractTransform(sourceComponent, _logger);
                if (transform != null)
                {
                    result = result with { Transform = transform };
                }
            }

            // 7b. Extract FeatureManager folder path (if analyzing from assembly selection)
            string? featureManagerFolderPath = null;
            if (includeComponentFolders && sourceComponent != null && analyzedFromSelection != null)
            {
                if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    var assemblyDoc = (AssemblyDoc)model;
                    featureManagerFolderPath = PartAnalysisContextSupport.TryGetFolderPathForComponentInstancePath(
                        app,
                        model,
                        assemblyDoc,
                        analyzedFromSelection,
                        openReferencedDocs,
                        out openedFolderDoc,
                        out openedFolderDocByUs);
                }
            }

            // 8. Add context-aware metadata
            result = result with
            {
                AnalyzedFromSelection = analyzedFromSelection,
                FeatureManagerFolderPath = featureManagerFolderPath
            };

            // 9. Handle OutputPath - save to file and return summary (aligned with analyze_sketch / analyze_assembly)
            var outputPath = GetStringParamNullable(parameters, "OutputPath");
            if (!string.IsNullOrEmpty(outputPath))
            {
                if (!TryWriteJsonToFile(outputPath, result, AnalysisCamelCaseJsonOptions, out var fileSizeBytes, out var errorMessage))
                {
                    _logger.LogError("Failed to write part analysis to file: {Path}. Error: {Error}", outputPath, errorMessage);
                    return Task.FromResult(ExecutionResult.Failure($"Failed to write to file: {errorMessage}"));
                }

                var summary = new
                {
                    Status = "Success",
                    OutputFile = outputPath,
                    FileSizeBytes = fileSizeBytes,
                    Summary = new
                    {
                        PartName = result.Metadata.Name,
                        FieldsMode = result.FieldsMode,
                        TotalFeatures = result.TotalFeatureCount,
                        IncludedFeatures = result.FilteredFeatureCount,
                        BodyCount = result.Bodies.Count,
                        HasMassProperties = result.MassProperties != null,
                        AnalyzedFromSelection = result.AnalyzedFromSelection
                    },
                    Message = $"Full analysis saved to {outputPath}. Use Read tool to view file contents."
                };

                return Task.FromResult(ExecutionResult.SuccessResult(summary, "Part analysis saved to file"));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(result));
        }
        finally
        {
            AnalysisHelpers.CloseDocIfOpenedByUs(app, openedDoc, openedByUs);
            AnalysisHelpers.CloseDocIfOpenedByUs(app, openedFolderDoc, openedFolderDocByUs);
        }
    }

    /// <summary>
    /// Get mass properties for the current active document (standalone operation)
    /// Works with both parts and assemblies
    /// </summary>
    private Task<ExecutionResult> GetMassPropertiesAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        var massProperties = AnalysisHelpers.ExtractMassProperties(model, _logger);
        if (massProperties == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to extract mass properties"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(massProperties));
    }

    #region Internal Analysis Methods

    /// <summary>
    /// Internal method to analyze a part (used by both single and batch operations)
    /// </summary>
    internal PartAnalysisResult AnalyzePartInternal(
        ModelDoc2 targetModel,
        PartDoc targetPart,
        string fields,
        bool includeFeatures,
        bool includeMassProperties,
        bool includeBodies,
        bool includeCustomProperties)
    {
        bool minimalMode = fields == "minimal";
        // Note: minimalMode affects data richness (ExtractFeaturesMinimal vs ExtractFeatures)
        // but does NOT override user's explicit include* flags anymore

        var result = new PartAnalysisResult();

        // OPT-3: Extract features first so we have the count available for metadata,
        // avoiding a second feature-tree traversal inside ExtractPartMetadata.
        int totalFeatureCount;
        List<PartFeature>? features = null;
        if (includeFeatures)
        {
            features = minimalMode
                ? AnalysisHelpers.ExtractFeaturesMinimal(targetModel)
                : AnalysisHelpers.ExtractFeatures(targetModel);
            totalFeatureCount = features.Count;
        }
        else
        {
            totalFeatureCount = AnalysisHelpers.CountFeatures(targetModel);
        }

        // OPT-4: Extract bodies first so we have the count available for metadata,
        // avoiding a second GetBodies2 call inside ExtractPartMetadata.
        List<PartBody>? bodies = null;
        int? precomputedBodyCount = null;
        if (includeBodies)
        {
            bodies = ExtractBodies(targetPart);
            precomputedBodyCount = bodies.Count;
        }

        // 1. Extract Metadata (always) — pass pre-computed counts to skip redundant traversals.
        result = result with { Metadata = ExtractPartMetadata(targetModel, targetPart, totalFeatureCount, precomputedBodyCount) };

        // 2. Attach pre-extracted features.
        if (features != null)
        {
            result = result with { Features = features };
        }

        // 3. Extract Mass Properties
        if (includeMassProperties)
        {
            result = result with
            {
                MassProperties = AnalysisHelpers.ExtractMassProperties(targetModel, _logger),
                BoundingBox = ExtractBoundingBox(targetPart)
            };
        }

        // 4. Attach pre-extracted bodies.
        if (bodies != null)
        {
            result = result with { Bodies = bodies };
        }

        // 5. Extract Custom Properties
        if (includeCustomProperties)
        {
            result = result with { CustomProperties = AnalysisHelpers.ExtractCustomProperties(targetModel, _logger) };

            // Extract configuration-specific custom properties (non-minimal only)
            if (!minimalMode)
            {
                var configProps = AnalysisExtractionSupport.ExtractConfigurationCustomProperties(targetModel, _logger);
                if (configProps.Count > 0)
                {
                    result = result with { ConfigurationCustomProperties = configProps };
                }
            }
        }

        // 6. Extract Summary Info (non-minimal only)
        if (!minimalMode)
        {
            result = result with { SummaryInfo = AnalysisExtractionSupport.ExtractSummaryInfo(targetModel, _logger) };
        }

        result = result with
        {
            FieldsMode = fields,
            TotalFeatureCount = totalFeatureCount,
            FilteredFeatureCount = result.Features.Count
        };

        return result;
    }

    #endregion

    #region Extraction Methods

    /// <summary>
    /// Extracts part metadata. Accepts optional pre-computed counts to avoid redundant
    /// COM traversals when features and bodies have already been extracted by the caller.
    /// OPT-3: pass <paramref name="precomputedFeatureCount"/> to skip a second feature-tree walk.
    /// OPT-4: pass <paramref name="precomputedBodyCount"/> to skip a second GetBodies2 call.
    /// </summary>
    private PartMetadata ExtractPartMetadata(
        ModelDoc2 model,
        PartDoc part,
        int? precomputedFeatureCount = null,
        int? precomputedBodyCount = null)
    {
        var configMgr = model.ConfigurationManager;
        var activeConfig = (Configuration?)configMgr.ActiveConfiguration;

        // OPT-4: Use pre-computed body count when available to avoid a duplicate GetBodies2 call.
        var bodyCount = precomputedBodyCount ?? part.GetBodies2((int)swBodyType_e.swAllBodies, false).SafeArrayCount();

        // OPT-3: Use pre-computed feature count when available to avoid a duplicate feature-tree traversal.
        var featureCount = precomputedFeatureCount ?? AnalysisHelpers.CountFeatures(model);

        return new PartMetadata
        {
            Name = model.GetTitle(),
            Path = model.GetPathName() ?? string.Empty,
            Type = "Part",
            ConfigurationName = activeConfig?.Name ?? "Default",
            TotalFeatures = featureCount,
            TotalBodies = bodyCount,
            Units = "mm"
        };
    }

    private PartBoundingBox? ExtractBoundingBox(PartDoc part)
    {
        try
        {
            var box = part.GetPartBox(false).ToDoubleArraySafe();
            if (box == null || box.Length < 6)
            {
                return null;
            }

            return new PartBoundingBox
            {
                MinX = MetersToMm(box[0]),
                MinY = MetersToMm(box[1]),
                MinZ = MetersToMm(box[2]),
                MaxX = MetersToMm(box[3]),
                MaxY = MetersToMm(box[4]),
                MaxZ = MetersToMm(box[5]),
                Width = MetersToMm(box[3] - box[0]),
                Height = MetersToMm(box[4] - box[1]),
                Depth = MetersToMm(box[5] - box[2])
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract bounding box");
            return null;
        }
    }

    private List<PartBody> ExtractBodies(PartDoc part)
    {
        var bodies = new List<PartBody>();
        var bodyArray = part.GetBodies2((int)swBodyType_e.swAllBodies, false).ToObjectArraySafe();
        if (bodyArray == null)
        {
            return bodies;
        }

        foreach (var bodyObj in bodyArray)
        {
            var body = bodyObj as IBody2;
            if (body == null)
            {
                continue;
            }

            var bodyType = (swBodyType_e)body.GetType();

            // OPT-6: Read Array.Length directly on the COM SafeArray base type instead of
            // allocating a full managed object[] just to count elements.
            var faceCount = body.GetFaces() is Array fa ? fa.Length : 0;
            var edgeCount = body.GetEdges() is Array ea ? ea.Length : 0;
            var vertexCount = body.GetVertices() is Array va ? va.Length : 0;

            // TODO: Material property extraction needs API verification
            // GetMaterialPropertyName2() signature may differ between IBody2 and Body2
            var materialName = "None";

            bodies.Add(new PartBody
            {
                Name = body.Name ?? "Unnamed Body",
                Type = bodyType.ToString(),
                Visible = body.Visible,
                MaterialName = materialName,
                FaceCount = faceCount,
                EdgeCount = edgeCount,
                VertexCount = vertexCount
            });
        }

        return bodies;
    }

    #endregion
}
