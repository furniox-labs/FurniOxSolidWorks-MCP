using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchInspection;

internal sealed class SketchAnalysisInspectionOperations : OperationHandlerBase
{
    public SketchAnalysisInspectionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchAnalysisInspectionOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.AnalyzeSketch" => Task.FromResult(AnalyzeSketch(parameters)),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch analysis operation: {operation}"))
        };
    }

    private ExecutionResult AnalyzeSketch(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return ExecutionResult.Failure("Not connected to SolidWorks");
        }

        var model = app.ActiveDoc as ModelDoc2;
        if (model == null)
        {
            return ExecutionResult.Failure("No active document");
        }

        var sketch = ResolveTargetSketch(model, out var sketchName, out var analysisSource);
        if (sketch == null)
        {
            return ExecutionResult.Failure(
                "No sketch available. Either select a sketch feature in the tree, or enter sketch edit mode first.");
        }

        try
        {
            var fieldsMode = GetStringParam(parameters, "Fields", "standard").ToLowerInvariant();

            bool includePoints;
            bool includeSegments;
            bool includeRelations;
            bool includeDimensions;
            bool includeMetadata;
            bool includeConstructionGeometry;
            bool calculateStatistics;
            bool includeConnectivity;

            switch (fieldsMode)
            {
                case "minimal":
                    includeMetadata = true;
                    includePoints = false;
                    includeSegments = false;
                    includeRelations = false;
                    includeDimensions = false;
                    includeConstructionGeometry = false;
                    calculateStatistics = false;
                    includeConnectivity = false;
                    break;
                case "full":
                    includeMetadata = true;
                    includePoints = true;
                    includeSegments = true;
                    includeRelations = true;
                    includeDimensions = true;
                    includeConstructionGeometry = true;
                    calculateStatistics = true;
                    includeConnectivity = true;
                    break;
                default:
                    includePoints = GetBoolParam(parameters, "IncludePoints", true);
                    includeSegments = GetBoolParam(parameters, "IncludeSegments", true);
                    includeRelations = GetBoolParam(parameters, "IncludeRelations", true);
                    includeDimensions = GetBoolParam(parameters, "IncludeDimensions", true);
                    includeMetadata = GetBoolParam(parameters, "IncludeMetadata", true);
                    includeConstructionGeometry = GetBoolParam(parameters, "IncludeConstructionGeometry", true);
                    calculateStatistics = GetBoolParam(parameters, "CalculateStatistics", false);
                    includeConnectivity = GetBoolParam(parameters, "IncludeConnectivity", false);
                    break;
            }

            var outputPath = GetStringParam(parameters, "OutputPath", string.Empty);
            var gapToleranceMm = GetDoubleParam(parameters, "GapToleranceMm", 0.01);
            var connectivityIncludeConstruction = GetBoolParam(parameters, "ConnectivityIncludeConstruction", false);

            var result = new SketchAnalysisResult
            {
                AnalysisSource = analysisSource,
                FieldsMode = fieldsMode
            };

            if (includeMetadata)
            {
                result = result with { Metadata = SketchInspectionMetadataSupport.ExtractMetadata(sketch, model) };
            }

            List<Shared.Models.SketchSegment> segments = new();
            var needSegmentsForDownstream = includeSegments || includeRelations || includeConnectivity || calculateStatistics;
            if (needSegmentsForDownstream)
            {
                segments = SketchInspectionGeometrySupport.ExtractSegments(sketch, includeConstructionGeometry);
                if (includeSegments)
                {
                    result = result with { Segments = segments };
                }
            }

            if (includePoints)
            {
                result = result with { Points = SketchInspectionMetadataSupport.ExtractPoints(sketch) };
            }

            if (includeRelations)
            {
                result = result with { Relations = SketchInspectionRelationSupport.ExtractRelations(sketch, segments) };
            }

            if (includeDimensions)
            {
                result = result with { Dimensions = SketchInspectionDimensionSupport.ExtractDimensions(sketch, model, _logger) };
            }

            if (calculateStatistics)
            {
                result = result with { Statistics = SketchInspectionMetadataSupport.CalculateStatistics(segments) };
            }

            if (includeConnectivity)
            {
                result = result with
                {
                    Connectivity = SketchInspectionGeometrySupport.CalculateConnectivity(
                        segments,
                        gapToleranceMm,
                        connectivityIncludeConstruction)
                };
            }

            var message = analysisSource == "selected"
                ? $"Analyzed selected sketch '{sketchName}' (read-only, no edit mode)"
                : $"Analyzed active sketch '{sketchName}' (in edit mode)";

            if (!string.IsNullOrEmpty(outputPath))
            {
                if (!TryWriteJsonToFile(outputPath, result, AnalysisCamelCaseJsonOptions, out var fileSizeBytes, out var errorMessage))
                {
                    _logger.LogError("Failed to write sketch analysis to file: {Path}. Error: {Error}", outputPath, errorMessage);
                    return ExecutionResult.Failure($"Failed to write to file: {errorMessage}");
                }

                var summary = new
                {
                    SketchName = sketchName,
                    AnalysisSource = analysisSource,
                    FieldsMode = fieldsMode,
                    SegmentCount = result.Segments.Count,
                    PointCount = result.Points.Count,
                    RelationCount = result.Relations.Count,
                    DimensionCount = result.Dimensions.Count,
                    OpenEndpointCount = result.Connectivity?.OpenEndpointCount,
                    OutputPath = outputPath,
                    FileSizeBytes = fileSizeBytes,
                    Message = $"{message}. Full results saved to: {outputPath}"
                };

                return ExecutionResult.SuccessResult(summary, $"{message}. Saved to: {outputPath}");
            }

            return ExecutionResult.SuccessResult(result, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sketch '{SketchName}'", sketchName);
            return ExecutionResult.Failure($"Failed to analyze sketch: {ex.Message}");
        }
    }

    private static Sketch? ResolveTargetSketch(ModelDoc2 model, out string sketchName, out string analysisSource)
    {
        sketchName = string.Empty;
        analysisSource = string.Empty;

        var selectionManager = model.SelectionManager as ISelectionMgr;
        if (selectionManager != null && selectionManager.GetSelectedObjectCount2(-1) > 0)
        {
            var selectedObject = selectionManager.GetSelectedObject6(1, -1);
            if (selectedObject is IFeature selectedFeature)
            {
                var specificFeature = selectedFeature.GetSpecificFeature2();
                if (specificFeature is Sketch selectedSketch)
                {
                    sketchName = selectedFeature.Name;
                    analysisSource = "selected";
                    return selectedSketch;
                }
            }
        }

        var activeSketch = model.SketchManager.ActiveSketch as Sketch;
        if (activeSketch != null)
        {
            sketchName = ((IFeature)activeSketch).Name ?? "ActiveSketch";
            analysisSource = "active";
            return activeSketch;
        }

        return null;
    }
}
