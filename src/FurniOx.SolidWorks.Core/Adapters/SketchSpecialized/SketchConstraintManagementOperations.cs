using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwSketchRelation = SolidWorks.Interop.sldworks.SketchRelation;

namespace FurniOx.SolidWorks.Core.Adapters.SketchSpecialized;

internal sealed class SketchConstraintManagementOperations : OperationHandlerBase
{
    public SketchConstraintManagementOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchConstraintManagementOperations> logger)
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
            "Sketch.ListConstraints" => ListConstraintsAsync(parameters),
            "Sketch.DeleteConstraint" => DeleteConstraintAsync(parameters),
            "Sketch.DisplayConstraints" => DisplayConstraintsAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch constraint management operation: {operation}"))
        };
    }

    private Task<ExecutionResult> ListConstraintsAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSpecializedContextSupport.TryGetActiveSketch(_connection, out _, out _, out _, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var relationManager = activeSketch!.RelationManager;
        if (relationManager == null)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
            {
                ["message"] = "No relation manager available",
                ["constraints"] = new List<object>()
            }));
        }

        var relationsObject = relationManager.GetRelations(0);
        if (relationsObject == null)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
            {
                ["message"] = "No constraints in active sketch",
                ["constraints"] = new List<object>()
            }));
        }

        var relations = SketchSpecializedContextSupport.GetObjectArrayOrEmpty(relationsObject);
        var constraintList = new List<object>();

        foreach (var relationObject in relations)
        {
            if (relationObject is not SwSketchRelation relation)
            {
                continue;
            }

            var relationType = relation.GetRelationType();
            SketchSpecializedContextSupport.TryGetRelationTypeName(relationType, out var relationTypeName);

            object[]? entities = null;
            try
            {
                entities = relation.GetDefinitionEntities2().ToObjectArraySafe();
            }
            catch
            {
            }

            constraintList.Add(new
            {
                Type = relationTypeName,
                TypeCode = relationType,
                EntityCount = entities?.Length ?? 0,
                HasEntities = entities is { Length: > 0 }
            });
        }

        _logger.LogInformation("Listed {Count} constraints in active sketch", constraintList.Count);

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = $"Found {constraintList.Count} total constraints",
            ["constraintCount"] = constraintList.Count,
            ["constraints"] = constraintList
        }));
    }

    private Task<ExecutionResult> DisplayConstraintsAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSpecializedContextSupport.TryGetActiveSketch(_connection, out _, out var model, out _, out _, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var show = GetBoolParam(parameters, "Show", true);
        var success = model!.Extension.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swViewSketchRelations, 0, show);
        if (!success)
        {
            _logger.LogWarning("SetUserPreferenceToggle failed for constraint visibility");
            return Task.FromResult(ExecutionResult.Failure("Failed to toggle constraint visibility"));
        }

        _logger.LogInformation("Constraint visibility set to {Show}", show);

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = show ? "Constraints are now visible" : "Constraints are now hidden",
            ["visible"] = show
        }));
    }

    private Task<ExecutionResult> DeleteConstraintAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSpecializedContextSupport.TryGetActiveSketch(_connection, out _, out _, out _, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var constraintIndex = GetIntParam(parameters, "ConstraintIndex", -1);
        if (constraintIndex < 0)
        {
            return Task.FromResult(ExecutionResult.Failure("ConstraintIndex parameter is required"));
        }

        var relationManager = activeSketch!.RelationManager;
        if (relationManager == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No relation manager available"));
        }

        var relationsObject = relationManager.GetRelations(0);
        if (relationsObject == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No constraints in active sketch"));
        }

        var relations = SketchSpecializedContextSupport.GetObjectArrayOrEmpty(relationsObject);
        if (constraintIndex >= relations.Length)
        {
            return Task.FromResult(ExecutionResult.Failure($"ConstraintIndex {constraintIndex} out of range (0-{relations.Length - 1})"));
        }

        if (relations[constraintIndex] is not SwSketchRelation relation)
        {
            return Task.FromResult(ExecutionResult.Failure($"Invalid constraint at index {constraintIndex}"));
        }

        var relationType = relation.GetRelationType();
        SketchSpecializedContextSupport.TryGetRelationTypeName(relationType, out var relationTypeName);

        if (!relationManager.DeleteRelation(relation))
        {
            _logger.LogWarning("Failed to delete constraint at index {Index}", constraintIndex);
            return Task.FromResult(ExecutionResult.Failure("Failed to delete constraint"));
        }

        _logger.LogInformation("Deleted constraint {Type} at index {Index}", relationTypeName, constraintIndex);

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Constraint deleted successfully",
            ["constraintIndex"] = constraintIndex,
            ["constraintType"] = relationTypeName
        }));
    }
}
