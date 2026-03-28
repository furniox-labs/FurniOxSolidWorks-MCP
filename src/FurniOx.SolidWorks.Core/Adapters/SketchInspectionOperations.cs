using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.SketchInspection;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Dispatches sketch inspection operations to focused handlers.
/// </summary>
public class SketchInspectionOperations : OperationHandlerBase
{
    private readonly SketchSegmentInspectionOperations _segmentInspectionOperations;
    private readonly SketchAnalysisInspectionOperations _analysisInspectionOperations;

    internal SketchInspectionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchInspectionOperations> logger,
        SketchSegmentInspectionOperations segmentInspectionOperations,
        SketchAnalysisInspectionOperations analysisInspectionOperations)
        : base(connection, settings, logger)
    {
        _segmentInspectionOperations = segmentInspectionOperations;
        _analysisInspectionOperations = analysisInspectionOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.ListSketchSegments" or "Sketch.GetSketchSegmentInfo"
                => _segmentInspectionOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.AnalyzeSketch"
                => _analysisInspectionOperations.ExecuteAsync(operation, parameters, cancellationToken),

            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch inspection operation: {operation}"))
        };
    }
}
