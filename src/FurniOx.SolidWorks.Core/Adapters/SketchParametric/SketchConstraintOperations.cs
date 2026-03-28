using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.SketchParametric;

internal sealed class SketchConstraintOperations : OperationHandlerBase
{
    public SketchConstraintOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchConstraintOperations> logger)
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
            "Sketch.AddConstraint" => AddConstraintAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch constraint operation: {operation}"))
        };
    }

    private Task<ExecutionResult> AddConstraintAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchParametricContextSupport.TryGetActiveSketch(_connection, out _, out var model, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var constraintType = GetStringParam(parameters, "ConstraintType", string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(constraintType))
        {
            return Task.FromResult(ExecutionResult.Failure("ConstraintType parameter is required"));
        }

        var constraintCode = SketchParametricTypeSupport.MapConstraintTypeToCode(constraintType);
        if (constraintCode == null)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Unknown constraint type: {constraintType}. Valid types: fixed, coincident, horizontal, vertical, midpoint, collinear, perpendicular, parallel, equallength, tangent, samecurvelength, concentric, coradial"));
        }

        if (!parameters.TryGetValue("EntityIds", out var entityIdsValue) ||
            !SketchParametricContextSupport.TryParseEntityIds(entityIdsValue, out var entityIds, out errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "EntityIds parameter is required"));
        }

        var requiredEntities = SketchParametricTypeSupport.GetRequiredConstraintEntityCount(constraintType);
        if (entityIds.Length < requiredEntities)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Constraint '{constraintType}' requires {requiredEntities} entities, but {entityIds.Length} provided"));
        }

        if (!SketchParametricContextSupport.TrySelectConstraintEntities(model!, activeSketch!, entityIds, out var selectedCount, out errorMessage))
        {
            model!.ClearSelection2(true);
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to select entities"));
        }

        model!.SketchAddConstraints(constraintCode);
        model.ClearSelection2(true);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            ConstraintType = constraintType,
            ConstraintCode = constraintCode,
            EntitiesConstrained = selectedCount,
            EntityIds = entityIds
        }));
    }
}
