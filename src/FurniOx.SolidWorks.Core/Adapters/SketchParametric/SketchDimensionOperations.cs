using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.SketchParametric;

internal sealed class SketchDimensionOperations : OperationHandlerBase
{
    public SketchDimensionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchDimensionOperations> logger)
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
            "Sketch.AddDimension" => AddDimensionAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch dimension operation: {operation}"))
        };
    }

    private Task<ExecutionResult> AddDimensionAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchParametricContextSupport.TryGetActiveSketch(_connection, out var app, out var model, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var dimensionType = GetStringParam(parameters, "DimensionType", string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(dimensionType))
        {
            return Task.FromResult(ExecutionResult.Failure("DimensionType parameter is required. Valid types: distance, angle, diameter, radius"));
        }

        if (!SketchParametricTypeSupport.IsValidDimensionType(dimensionType))
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Unknown dimension type: {dimensionType}. Valid types: distance, angle, diameter, radius"));
        }

        if (!parameters.TryGetValue("EntityIds", out var entityIdsValue) ||
            !SketchParametricContextSupport.TryParseEntityIds(entityIdsValue, out var entityIds, out errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "EntityIds parameter is required"));
        }

        var requiredEntities = SketchParametricTypeSupport.GetRequiredDimensionEntityCount(dimensionType);
        if (entityIds.Length < requiredEntities)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Dimension type '{dimensionType}' requires {requiredEntities} entities, but {entityIds.Length} provided"));
        }

        var x = MmToMeters(GetDoubleParam(parameters, "X", 0.0));
        var y = MmToMeters(GetDoubleParam(parameters, "Y", 0.0));
        var z = MmToMeters(GetDoubleParam(parameters, "Z", 0.0));

        double? dimensionValue = null;
        if (parameters.TryGetValue("Value", out var valueParameter) && valueParameter != null)
        {
            dimensionValue = MmToMeters(Convert.ToDouble(valueParameter));
        }

        model!.ClearSelection2(true);
        if (!SketchParametricContextSupport.TryGetSegments(activeSketch!, out var segments, out errorMessage) ||
            !SketchParametricContextSupport.TrySelectDimensionSegments(segments, entityIds, out errorMessage))
        {
            model.ClearSelection2(true);
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Failed to select sketch segments"));
        }

        var originalInputDimValue = app!.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate);
        app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);

        try
        {
            var dimension = dimensionType == "diameter"
                ? (DisplayDimension?)model.AddDiameterDimension2(x, y, z)
                : (DisplayDimension?)model.AddDimension2(x, y, z);

            if (dimension == null)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    $"Failed to create {dimensionType} dimension. Verify entity selection is valid for this dimension type."));
            }

            if (dimensionValue.HasValue && dimension.GetDimension2(0) is Dimension dimensionObject)
            {
                var result = dimensionObject.SetSystemValue3(dimensionValue.Value, 0, null);
                if (result != 0)
                {
                    _logger.LogWarning("Failed to set dimension value. Error code: {Result}", result);
                }
            }

            model.ClearSelection2(true);
            model.EditRebuild3();

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                DimensionType = dimensionType,
                EntitiesDimensioned = entityIds.Length,
                EntityIds = entityIds,
                Placement = new { X = MetersToMm(x), Y = MetersToMm(y), Z = MetersToMm(z) },
                Value = dimensionValue.HasValue ? MetersToMm(dimensionValue.Value) : (double?)null,
                DrivingDimension = dimensionValue.HasValue
            }));
        }
        finally
        {
            model.ClearSelection2(true);
            app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, originalInputDimValue);
        }
    }
}
