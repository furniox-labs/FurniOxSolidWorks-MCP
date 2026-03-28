using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.SketchGeometry;

internal sealed class SketchArcGeometryOperations : OperationHandlerBase
{
    public SketchArcGeometryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchArcGeometryOperations> logger)
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
            "Sketch.SketchArc" => SketchArcAsync(parameters),
            "Sketch.Sketch3PointArc" => SketchThreePointArcAsync(parameters),
            "Sketch.SketchTangentArc" => SketchTangentArcAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch arc operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SketchArcAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var centerX = MmToMeters(GetDoubleParam(parameters, "CenterX"));
        var centerY = MmToMeters(GetDoubleParam(parameters, "CenterY"));
        var radius = MmToMeters(GetDoubleParam(parameters, "Radius", 10.0));
        var startAngle = GetDoubleParam(parameters, "StartAngle");
        var endAngle = GetDoubleParam(parameters, "EndAngle", 90.0);
        var clockwise = GetBoolParam(parameters, "Clockwise");

        var startAngleRadians = DegreesToRadians(startAngle);
        var endAngleRadians = DegreesToRadians(endAngle);
        var x1 = centerX + radius * Math.Cos(startAngleRadians);
        var y1 = centerY + radius * Math.Sin(startAngleRadians);
        var x2 = centerX + radius * Math.Cos(endAngleRadians);
        var y2 = centerY + radius * Math.Sin(endAngleRadians);

        var arc = model!.SketchManager.CreateArc(centerX, centerY, 0, x1, y1, 0, x2, y2, 0, (short)(clockwise ? -1 : 1));
        if (arc == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create arc"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            CenterX = MetersToMm(centerX),
            CenterY = MetersToMm(centerY),
            Radius = MetersToMm(radius),
            StartAngle = startAngle,
            EndAngle = endAngle,
            Clockwise = clockwise
        }));
    }

    private Task<ExecutionResult> SketchThreePointArcAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var x1 = MmToMeters(GetDoubleParam(parameters, "X1"));
        var y1 = MmToMeters(GetDoubleParam(parameters, "Y1"));
        var x2 = MmToMeters(GetDoubleParam(parameters, "X2", 10.0));
        var y2 = MmToMeters(GetDoubleParam(parameters, "Y2"));
        var x3 = MmToMeters(GetDoubleParam(parameters, "X3", 5.0));
        var y3 = MmToMeters(GetDoubleParam(parameters, "Y3", 10.0));

        var arc = model!.SketchManager.Create3PointArc(x1, y1, 0, x2, y2, 0, x3, y3, 0);
        if (arc == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create 3-point arc"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            X1 = MetersToMm(x1),
            Y1 = MetersToMm(y1),
            X2 = MetersToMm(x2),
            Y2 = MetersToMm(y2),
            X3 = MetersToMm(x3),
            Y3 = MetersToMm(y3)
        }));
    }

    private Task<ExecutionResult> SketchTangentArcAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var x1 = MmToMeters(GetDoubleParam(parameters, "X1"));
        var y1 = MmToMeters(GetDoubleParam(parameters, "Y1"));
        var x2 = MmToMeters(GetDoubleParam(parameters, "X2", 10.0));
        var y2 = MmToMeters(GetDoubleParam(parameters, "Y2"));
        var arcType = GetIntParam(parameters, "ArcType", 1);

        var arc = model!.SketchManager.CreateTangentArc(x1, y1, 0, x2, y2, 0, arcType);
        if (arc == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create tangent arc"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            X1 = MetersToMm(x1),
            Y1 = MetersToMm(y1),
            X2 = MetersToMm(x2),
            Y2 = MetersToMm(y2),
            ArcType = arcType,
            Type = "TangentArc"
        }));
    }
}
