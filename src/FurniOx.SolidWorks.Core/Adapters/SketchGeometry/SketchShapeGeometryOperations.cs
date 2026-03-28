using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.SketchGeometry;

internal sealed class SketchShapeGeometryOperations : OperationHandlerBase
{
    public SketchShapeGeometryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchShapeGeometryOperations> logger)
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
            "Sketch.SketchCornerRectangle" => SketchCornerRectangleAsync(parameters),
            "Sketch.SketchEllipse" => SketchEllipseAsync(parameters),
            "Sketch.SketchPolygon" => SketchPolygonAsync(parameters),
            "Sketch.SketchSpline" => SketchSplineAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch shape operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SketchCornerRectangleAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var x1 = MmToMeters(GetDoubleParam(parameters, "X1"));
        var y1 = MmToMeters(GetDoubleParam(parameters, "Y1"));
        var x2 = MmToMeters(GetDoubleParam(parameters, "X2", 10.0));
        var y2 = MmToMeters(GetDoubleParam(parameters, "Y2", 10.0));

        var segments = model!.SketchManager.CreateCornerRectangle(x1, y1, 0, x2, y2, 0);
        if (segments == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create corner rectangle"));
        }

        var segmentArray = segments.ToObjectArraySafe();
        if (segmentArray == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to retrieve rectangle segments"));
        }

        model.ClearSelection2(true);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            X1 = MetersToMm(x1),
            Y1 = MetersToMm(y1),
            X2 = MetersToMm(x2),
            Y2 = MetersToMm(y2),
            SegmentCount = segmentArray.Length,
            Type = "CornerRectangle"
        }));
    }

    private Task<ExecutionResult> SketchEllipseAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var xc = MmToMeters(GetDoubleParam(parameters, "Xc"));
        var yc = MmToMeters(GetDoubleParam(parameters, "Yc"));
        var xMajor = MmToMeters(GetDoubleParam(parameters, "Xmaj", 20.0));
        var yMajor = MmToMeters(GetDoubleParam(parameters, "Ymaj"));
        var xMinor = MmToMeters(GetDoubleParam(parameters, "Xmin"));
        var yMinor = MmToMeters(GetDoubleParam(parameters, "Ymin", 10.0));

        var ellipse = model!.SketchManager.CreateEllipse(xc, yc, 0, xMajor, yMajor, 0, xMinor, yMinor, 0);
        if (ellipse == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create ellipse"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Center = new { X = MetersToMm(xc), Y = MetersToMm(yc) },
            MajorAxis = new { X = MetersToMm(xMajor), Y = MetersToMm(yMajor) },
            MinorAxis = new { X = MetersToMm(xMinor), Y = MetersToMm(yMinor) },
            Type = "Ellipse"
        }));
    }

    private Task<ExecutionResult> SketchPolygonAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var xc = MmToMeters(GetDoubleParam(parameters, "Xc"));
        var yc = MmToMeters(GetDoubleParam(parameters, "Yc"));
        var xp = MmToMeters(GetDoubleParam(parameters, "Xp", 20.0));
        var yp = MmToMeters(GetDoubleParam(parameters, "Yp"));
        var sides = GetIntParam(parameters, "Sides", 6);
        var inscribed = GetBoolParam(parameters, "Inscribed", true);

        if (sides < 3)
        {
            return Task.FromResult(ExecutionResult.Failure("Polygon must have at least 3 sides"));
        }

        var segments = model!.SketchManager.CreatePolygon(xc, yc, 0, xp, yp, 0, sides, inscribed);
        if (segments == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create polygon"));
        }

        var segmentArray = segments.ToObjectArraySafe();
        if (segmentArray == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to retrieve polygon segments"));
        }

        model.ClearSelection2(true);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Center = new { X = MetersToMm(xc), Y = MetersToMm(yc) },
            Vertex = new { X = MetersToMm(xp), Y = MetersToMm(yp) },
            Sides = sides,
            Inscribed = inscribed,
            SegmentCount = segmentArray.Length,
            Type = "Polygon"
        }));
    }

    private Task<ExecutionResult> SketchSplineAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        if (!parameters.TryGetValue("Points", out var pointsValue) || pointsValue == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Points parameter is required"));
        }

        var points = SketchGeometryContextSupport.ParsePointArray(pointsValue);
        if (points == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Points must be an array of doubles"));
        }

        if (points.Length < 6 || points.Length % 3 != 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Points array must contain at least 6 values (2 points) and be divisible by 3"));
        }

        for (var index = 0; index < points.Length; index++)
        {
            points[index] = MmToMeters(points[index]);
        }

        var spline = model!.SketchManager.CreateSpline(points);
        if (spline == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create spline"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            PointCount = points.Length / 3,
            Points = points.Select(MetersToMm).ToArray(),
            Type = "Spline"
        }));
    }
}
