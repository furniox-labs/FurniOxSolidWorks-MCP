using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.SketchGeometry;

internal sealed class SketchPrimitiveGeometryOperations : OperationHandlerBase
{
    public SketchPrimitiveGeometryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchPrimitiveGeometryOperations> logger)
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
            "Sketch.SketchCircle" => SketchCircleAsync(parameters),
            "Sketch.SketchLine" => SketchLineAsync(parameters),
            "Sketch.SketchCenterLine" => SketchCenterLineAsync(parameters),
            "Sketch.SketchPoint" => SketchPointAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch primitive operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SketchCircleAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var centerX = MmToMeters(GetDoubleParam(parameters, "CenterX"));
        var centerY = MmToMeters(GetDoubleParam(parameters, "CenterY"));
        var radius = MmToMeters(GetDoubleParam(parameters, "Radius", 10.0));

        var circle = model!.SketchManager.CreateCircleByRadius(centerX, centerY, 0, radius);
        if (circle == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create circle"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            CenterX = MetersToMm(centerX),
            CenterY = MetersToMm(centerY),
            Radius = MetersToMm(radius)
        }));
    }

    private Task<ExecutionResult> SketchLineAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var x1 = MmToMeters(GetDoubleParam(parameters, "X1"));
        var y1 = MmToMeters(GetDoubleParam(parameters, "Y1"));
        var x2 = MmToMeters(GetDoubleParam(parameters, "X2", 10.0));
        var y2 = MmToMeters(GetDoubleParam(parameters, "Y2", 10.0));

        var line = model!.SketchManager.CreateLine(x1, y1, 0, x2, y2, 0);
        if (line == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create line"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            X1 = MetersToMm(x1),
            Y1 = MetersToMm(y1),
            X2 = MetersToMm(x2),
            Y2 = MetersToMm(y2)
        }));
    }

    private Task<ExecutionResult> SketchCenterLineAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var x1 = MmToMeters(GetDoubleParam(parameters, "X1"));
        var y1 = MmToMeters(GetDoubleParam(parameters, "Y1"));
        var x2 = MmToMeters(GetDoubleParam(parameters, "X2", 10.0));
        var y2 = MmToMeters(GetDoubleParam(parameters, "Y2", 10.0));

        var centerLine = model!.SketchManager.CreateCenterLine(x1, y1, 0, x2, y2, 0);
        if (centerLine == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create centerline"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            X1 = MetersToMm(x1),
            Y1 = MetersToMm(y1),
            X2 = MetersToMm(x2),
            Y2 = MetersToMm(y2),
            Type = "CenterLine"
        }));
    }

    private Task<ExecutionResult> SketchPointAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var x = MmToMeters(GetDoubleParam(parameters, "X"));
        var y = MmToMeters(GetDoubleParam(parameters, "Y"));
        var z = MmToMeters(GetDoubleParam(parameters, "Z"));

        var point = model!.SketchManager.CreatePoint(x, y, z);
        if (point == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create point"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            X = MetersToMm(x),
            Y = MetersToMm(y),
            Z = MetersToMm(z),
            Type = "Point"
        }));
    }
}
