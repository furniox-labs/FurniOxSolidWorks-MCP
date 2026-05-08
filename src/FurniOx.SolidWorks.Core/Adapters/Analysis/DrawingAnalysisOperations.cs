using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

/// <summary>
/// Handles drawing analysis operations.
/// </summary>
public class DrawingAnalysisOperations : OperationHandlerBase
{
    public DrawingAnalysisOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DrawingAnalysisOperations> logger)
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
            "Analysis.AnalyzeDrawing" => await AnalyzeDrawingAsync(parameters, cancellationToken),
            _ => ExecutionResult.Failure($"Unknown drawing analysis operation: {operation}")
        };
    }

    private Task<ExecutionResult> AnalyzeDrawingAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = app.ActiveDoc as ModelDoc2;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        if (model is not DrawingDoc drawingDoc)
        {
            return Task.FromResult(ExecutionResult.Failure("Active document is not a drawing"));
        }

        var includeSheets = GetBoolParam(parameters, "IncludeSheets", true);
        var includeViews = GetBoolParam(parameters, "IncludeViews", true);
        var includeAnnotations = GetBoolParam(parameters, "IncludeAnnotations", true);
        var includeBomTables = GetBoolParam(parameters, "IncludeBomTables", true);
        var includeReferencedModels = GetBoolParam(parameters, "IncludeReferencedModels", true);
        var includeCustomProperties = GetBoolParam(parameters, "IncludeCustomProperties", true);

        var result = new DrawingAnalysisResult
        {
            Metadata = Drawing.DrawingMetadataSupport.Extract(model, drawingDoc)
        };

        if (includeSheets)
        {
            result = result with { Sheets = Drawing.DrawingSheetSupport.Extract(drawingDoc, _logger) };
        }

        if (includeViews)
        {
            result = result with { Views = Drawing.DrawingViewSupport.Extract(drawingDoc, _logger) };
        }

        if (includeAnnotations)
        {
            result = result with { Annotations = Drawing.DrawingAnnotationSupport.Extract(drawingDoc, _logger) };
        }

        if (includeBomTables)
        {
            result = result with { BomTables = Drawing.DrawingBomSupport.Extract(drawingDoc, _logger) };
        }

        if (includeReferencedModels)
        {
            result = result with { ReferencedModels = Drawing.DrawingReferencedModelSupport.Extract(drawingDoc, _logger) };
        }

        if (includeCustomProperties)
        {
            result = result with { CustomProperties = AnalysisHelpers.ExtractCustomProperties(model, _logger) };

            var configProps = AnalysisExtractionSupport.ExtractConfigurationCustomProperties(model, _logger);
            if (configProps.Count > 0)
            {
                result = result with { ConfigurationCustomProperties = configProps };
            }
        }

        result = result with { SummaryInfo = AnalysisExtractionSupport.ExtractSummaryInfo(model, _logger) };

        return Task.FromResult(ExecutionResult.SuccessResult(result));
    }
}
