using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.SketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchAdvanced;

internal sealed class SketchThreeDimensionalOperations : OperationHandlerBase
{
    public SketchThreeDimensionalOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchThreeDimensionalOperations> logger)
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
            "Sketch.Create3DSketch" => Create3DSketchAsync(parameters),
            "Sketch.Sketch3DLine" => Sketch3DLineAsync(parameters),
            "Sketch.Sketch3DSpline" => Sketch3DSplineAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown 3D sketch operation: {operation}"))
        };
    }

    private Task<ExecutionResult> Create3DSketchAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetModel(out _, out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var activeSketch = sketchManager!.ActiveSketch as Sketch;
        var wasIn3DSketch = activeSketch != null && activeSketch.Is3D();
        var toggle = GetBoolParam(parameters, "Toggle", true);

        sketchManager.Insert3DSketch(toggle);

        activeSketch = sketchManager.ActiveSketch as Sketch;
        var isNow3DSketch = activeSketch != null && activeSketch.Is3D();
        var message = !wasIn3DSketch && isNow3DSketch
            ? "Entered 3D sketch mode"
            : wasIn3DSketch && !isNow3DSketch
                ? "Exited 3D sketch mode"
                : isNow3DSketch
                    ? "Already in 3D sketch mode"
                    : "No active sketch";

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = message,
            ["is3DSketch"] = isNow3DSketch,
            ["hasActiveSketch"] = activeSketch != null
        }));
    }

    private Task<ExecutionResult> Sketch3DLineAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetThreeDimensionalSketch(out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Active sketch must be 3D"));
        }

        var x1 = MmToMeters(GetDoubleParam(parameters, "X1", 0.0));
        var y1 = MmToMeters(GetDoubleParam(parameters, "Y1", 0.0));
        var z1 = MmToMeters(GetDoubleParam(parameters, "Z1", 0.0));
        var x2 = MmToMeters(GetDoubleParam(parameters, "X2", 100.0));
        var y2 = MmToMeters(GetDoubleParam(parameters, "Y2", 50.0));
        var z2 = MmToMeters(GetDoubleParam(parameters, "Z2", 80.0));

        var line = sketchManager!.CreateLine(x1, y1, z1, x2, y2, z2) as SwSketchSegment;
        if (line == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create 3D line - verify 3D sketch mode"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "3D line created successfully",
            ["startPoint_mm"] = new[] { x1 * 1000, y1 * 1000, z1 * 1000 },
            ["endPoint_mm"] = new[] { x2 * 1000, y2 * 1000, z2 * 1000 },
            ["length_mm"] = line.GetLength() * 1000
        }));
    }

    private Task<ExecutionResult> Sketch3DSplineAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetThreeDimensionalSketch(out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "Active sketch must be 3D"));
        }

        if (!parameters.TryGetValue("Points", out var pointsValue) || pointsValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Points parameter is required (double[] array)"));
        }

        var pointArray = SketchAdvancedContextSupport.ParsePointArray(pointsValue);
        if (pointArray == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Points must be double[] array in format [x1,y1,z1,x2,y2,z2,...]"));
        }

        if (pointArray.Length < 6 || pointArray.Length % 3 != 0)
        {
            return Task.FromResult(ExecutionResult.Failure($"Points array invalid length {pointArray.Length} - must be multiple of 3 with minimum 6 elements (2 points)"));
        }

        var simulateNaturalEnds = GetBoolParam(parameters, "SimulateNaturalEnds", true);
        var spline = sketchManager!.CreateSpline2(pointArray, simulateNaturalEnds) as SwSketchSegment;
        if (spline == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create 3D spline - verify 3D sketch mode and points"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "3D spline created successfully",
            ["pointCount"] = pointArray.Length / 3,
            ["simulateNaturalEnds"] = simulateNaturalEnds
        }));
    }

    private bool TryGetModel(out ModelDoc2? model, out SketchManager? sketchManager, out string? errorMessage)
    {
        errorMessage = null;
        model = null;
        sketchManager = null;

        var app = _connection.Application;
        if (app == null)
        {
            errorMessage = "Not connected to SolidWorks";
            return false;
        }

        model = app.ActiveDoc as ModelDoc2;
        if (model == null)
        {
            errorMessage = "No active document";
            return false;
        }

        sketchManager = model.SketchManager;
        return true;
    }

    private bool TryGetThreeDimensionalSketch(out SketchManager? sketchManager, out string? errorMessage)
    {
        if (!TryGetModel(out _, out sketchManager, out errorMessage))
        {
            return false;
        }

        var activeSketch = sketchManager!.ActiveSketch as Sketch;
        if (activeSketch == null)
        {
            errorMessage = "No active sketch - use Create3DSketch first";
            return false;
        }

        if (!activeSketch.Is3D())
        {
            errorMessage = "Active sketch must be 3D - use Create3DSketch first";
            return false;
        }

        return true;
    }
}
