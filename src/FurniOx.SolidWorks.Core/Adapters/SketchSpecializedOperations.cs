using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.SketchSpecialized;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Dispatches specialized sketch operations to focused handlers.
/// </summary>
public class SketchSpecializedOperations : OperationHandlerBase
{
    private readonly SketchBlockOperations _blockOperations;
    private readonly SketchConstraintManagementOperations _constraintManagementOperations;
    private readonly SketchTextOperations _textOperations;

    internal SketchSpecializedOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchSpecializedOperations> logger,
        SketchBlockOperations blockOperations,
        SketchConstraintManagementOperations constraintManagementOperations,
        SketchTextOperations textOperations)
        : base(connection, settings, logger)
    {
        _blockOperations = blockOperations;
        _constraintManagementOperations = constraintManagementOperations;
        _textOperations = textOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Sketch.InsertBlock" or "Sketch.MakeBlock" or "Sketch.ExplodeBlock" or "Sketch.SaveBlock"
                => _blockOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.ListConstraints" or "Sketch.DeleteConstraint" or "Sketch.DisplayConstraints"
                => _constraintManagementOperations.ExecuteAsync(operation, parameters, cancellationToken),

            "Sketch.SketchText" or "Sketch.SketchTextOnPath" or "Sketch.SketchSymbol"
                => _textOperations.ExecuteAsync(operation, parameters, cancellationToken),

            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch operation: {operation}"))
        };
    }
}
