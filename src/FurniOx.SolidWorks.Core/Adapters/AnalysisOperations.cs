using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Analysis;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Single-document analysis operations coordinator. Batch analysis lives separately in
/// BatchAnalysisOperations (private build only).
/// </summary>
public class AnalysisOperations : OperationHandlerBase
{
    private readonly PartAnalysisOperations _partOps;
    private readonly AssemblyAnalysisOperations _assemblyOps;
    private readonly DrawingAnalysisOperations _drawingOps;

    public AnalysisOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<AnalysisOperations> logger,
        PartAnalysisOperations partOps,
        AssemblyAnalysisOperations assemblyOps,
        DrawingAnalysisOperations drawingOps)
        : base(connection, settings, logger)
    {
        _partOps = partOps;
        _assemblyOps = assemblyOps;
        _drawingOps = drawingOps;
    }

    public override async Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Analysis.AnalyzePart" => await _partOps.ExecuteAsync(operation, parameters, cancellationToken),
            "Analysis.GetMassProperties" => await _partOps.ExecuteAsync(operation, parameters, cancellationToken),
            "Analysis.AnalyzeAssembly" => await _assemblyOps.ExecuteAsync(operation, parameters, cancellationToken),
            "Analysis.AnalyzeDrawing" => await _drawingOps.ExecuteAsync(operation, parameters, cancellationToken),
            _ => ExecutionResult.Failure($"Unknown analysis operation: {operation}")
        };
    }
}
