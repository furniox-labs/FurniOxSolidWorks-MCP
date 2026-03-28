using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.SketchParametric;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Dispatches parametric sketch operations to focused handlers.
/// </summary>
public class SketchParametricOperations : OperationHandlerBase
{
    private readonly SketchConstraintOperations _constraintOperations;
    private readonly SketchDimensionOperations _dimensionOperations;

    internal SketchParametricOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchParametricOperations> logger,
        SketchConstraintOperations constraintOperations,
        SketchDimensionOperations dimensionOperations)
        : base(connection, settings, logger)
    {
        _constraintOperations = constraintOperations;
        _dimensionOperations = dimensionOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.AddConstraint" => _constraintOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Sketch.AddDimension" => _dimensionOperations.ExecuteAsync(operation, parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown parametric sketch operation: {operation}"))
        };
    }
}
