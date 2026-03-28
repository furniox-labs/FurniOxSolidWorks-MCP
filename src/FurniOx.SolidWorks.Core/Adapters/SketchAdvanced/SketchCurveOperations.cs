using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.SketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchAdvanced;

internal sealed class SketchCurveOperations : OperationHandlerBase
{
    public SketchCurveOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchCurveOperations> logger)
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
            "Sketch.SketchParabola" => SketchParabolaAsync(parameters),
            "Sketch.SketchConic" => SketchConicAsync(parameters),
            "Sketch.SketchHexagon" => SketchHexagonAsync(parameters),
            "Sketch.SketchBezier" => SketchBezierAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch curve operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SketchParabolaAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var focusX = MmToMeters(GetDoubleParam(parameters, "FocusX", 10.0));
        var focusY = MmToMeters(GetDoubleParam(parameters, "FocusY", 0.0));
        var focusZ = MmToMeters(GetDoubleParam(parameters, "FocusZ", 0.0));
        var apexX = MmToMeters(GetDoubleParam(parameters, "ApexX", 0.0));
        var apexY = MmToMeters(GetDoubleParam(parameters, "ApexY", 0.0));
        var apexZ = MmToMeters(GetDoubleParam(parameters, "ApexZ", 0.0));
        var endPoint1X = MmToMeters(GetDoubleParam(parameters, "EndPt1X", -50.0));
        var endPoint1Y = MmToMeters(GetDoubleParam(parameters, "EndPt1Y", 50.0));
        var endPoint1Z = MmToMeters(GetDoubleParam(parameters, "EndPt1Z", 0.0));
        var endPoint2X = MmToMeters(GetDoubleParam(parameters, "EndPt2X", 50.0));
        var endPoint2Y = MmToMeters(GetDoubleParam(parameters, "EndPt2Y", 50.0));
        var endPoint2Z = MmToMeters(GetDoubleParam(parameters, "EndPt2Z", 0.0));

        var parabola = sketchManager!.CreateParabola(
            focusX, focusY, focusZ,
            apexX, apexY, apexZ,
            endPoint1X, endPoint1Y, endPoint1Z,
            endPoint2X, endPoint2Y, endPoint2Z) as SwSketchSegment;

        if (parabola == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create parabola - check parameters"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Parabola created successfully",
            ["focusPoint_mm"] = new[] { focusX * 1000, focusY * 1000, focusZ * 1000 },
            ["apexPoint_mm"] = new[] { apexX * 1000, apexY * 1000, apexZ * 1000 }
        }));
    }

    private Task<ExecutionResult> SketchConicAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var startX = MmToMeters(GetDoubleParam(parameters, "StartX", 0.0));
        var startY = MmToMeters(GetDoubleParam(parameters, "StartY", 0.0));
        var startZ = MmToMeters(GetDoubleParam(parameters, "StartZ", 0.0));
        var endX = MmToMeters(GetDoubleParam(parameters, "EndX", 100.0));
        var endY = MmToMeters(GetDoubleParam(parameters, "EndY", 0.0));
        var endZ = MmToMeters(GetDoubleParam(parameters, "EndZ", 0.0));
        var apexX = MmToMeters(GetDoubleParam(parameters, "ApexX", 50.0));
        var apexY = MmToMeters(GetDoubleParam(parameters, "ApexY", 25.0));
        var apexZ = MmToMeters(GetDoubleParam(parameters, "ApexZ", 0.0));
        var param10 = GetDoubleParam(parameters, "Param10", 0.0);
        var param11 = GetDoubleParam(parameters, "Param11", 0.0);
        var rho = GetDoubleParam(parameters, "Rho", 0.5);

        var conic = sketchManager!.CreateConic(
            startX, startY, startZ,
            endX, endY, endZ,
            apexX, apexY, apexZ,
            param10, param11,
            rho) as SwSketchSegment;

        if (conic == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create conic - check parameters"));
        }

        var conicType = rho < 0.5 ? "ellipse" : rho == 0.5 ? "parabola" : "hyperbola";
        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Conic created successfully",
            ["conicType"] = conicType,
            ["rho"] = rho,
            ["startPoint_mm"] = new[] { startX * 1000, startY * 1000, startZ * 1000 },
            ["endPoint_mm"] = new[] { endX * 1000, endY * 1000, endZ * 1000 }
        }));
    }

    private Task<ExecutionResult> SketchHexagonAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var centerX = MmToMeters(GetDoubleParam(parameters, "CenterX", 0.0));
        var centerY = MmToMeters(GetDoubleParam(parameters, "CenterY", 0.0));
        var centerZ = MmToMeters(GetDoubleParam(parameters, "CenterZ", 0.0));
        var vertexX = MmToMeters(GetDoubleParam(parameters, "VertexX", 50.0));
        var vertexY = MmToMeters(GetDoubleParam(parameters, "VertexY", 0.0));
        var vertexZ = MmToMeters(GetDoubleParam(parameters, "VertexZ", 0.0));
        var inscribed = GetBoolParam(parameters, "Inscribed", true);

        var polygonObject = sketchManager!.CreatePolygon(
            centerX,
            centerY,
            centerZ,
            vertexX,
            vertexY,
            vertexZ,
            6,
            inscribed);

        var segments = polygonObject?.ToObjectArraySafe();
        if (segments == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create hexagon - check parameters"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Hexagon created successfully",
            ["sides"] = 6,
            ["segmentCount"] = segments.Length,
            ["inscribed"] = inscribed,
            ["center_mm"] = new[] { centerX * 1000, centerY * 1000, centerZ * 1000 }
        }));
    }

    private Task<ExecutionResult> SketchBezierAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var sketchManager, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        if (!parameters.TryGetValue("Points", out var pointsValue) || pointsValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Points parameter is required"));
        }

        var pointArray = SketchAdvancedContextSupport.ParsePointArray(pointsValue);
        if (pointArray == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Points must be double[] array"));
        }

        if (pointArray.Length < 6 || pointArray.Length % 3 != 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Points array must be multiple of 3 with minimum 6 elements"));
        }

        var simulateNaturalEnds = GetBoolParam(parameters, "SimulateNaturalEnds", true);
        var spline = sketchManager!.CreateSpline2(pointArray, simulateNaturalEnds) as SwSketchSegment;
        if (spline == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create Bezier-approximation spline"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Bezier-approximation spline created (no dedicated Bezier API exists)",
            ["method"] = "CreateSpline2",
            ["note"] = "B-splines are more general than Bezier; use IModeler.CreateBsplineCurve for true Bezier",
            ["pointCount"] = pointArray.Length / 3,
            ["simulateNaturalEnds"] = simulateNaturalEnds
        }));
    }

    private bool TryGetActiveSketch(out SketchManager? sketchManager, out string? errorMessage)
    {
        errorMessage = null;
        sketchManager = null;

        var app = _connection.Application;
        if (app == null)
        {
            errorMessage = "Not connected to SolidWorks";
            return false;
        }

        var model = app.ActiveDoc as ModelDoc2;
        if (model == null)
        {
            errorMessage = "No active document";
            return false;
        }

        sketchManager = model.SketchManager;
        if (sketchManager.ActiveSketch is not Sketch)
        {
            errorMessage = "No active sketch";
            return false;
        }

        return true;
    }
}
