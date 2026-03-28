using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.ISketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchInspection;

internal sealed class SketchSegmentInspectionOperations : OperationHandlerBase
{
    public SketchSegmentInspectionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchSegmentInspectionOperations> logger)
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
            "Sketch.ListSketchSegments" => ListSketchSegmentsAsync(),
            "Sketch.GetSketchSegmentInfo" => GetSketchSegmentInfoAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch segment inspection operation: {operation}"))
        };
    }

    private Task<ExecutionResult> ListSketchSegmentsAsync()
    {
        if (!TryGetActiveSketch(out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var segmentsObject = activeSketch!.GetSketchSegments();
        if (segmentsObject == null)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                SegmentCount = 0,
                Segments = new object[0],
                Message = "Active sketch has no segments"
            }));
        }

        var segments = segmentsObject.ToObjectArraySafe();
        if (segments == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to retrieve sketch segments"));
        }

        var segmentList = new List<object>();
        foreach (var segmentObject in segments)
        {
            if (segmentObject is not SwSketchSegment segment)
            {
                continue;
            }

            segmentList.Add(new
            {
                Type = SketchInspectionSegmentSupport.GetSegmentTypeName(segment),
                ConstructionGeometry = segment.ConstructionGeometry,
                Length = segment.GetLength() > 0 ? MetersToMm(segment.GetLength()) : 0.0,
                Id = (int[])segment.GetID()
            });
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            SegmentCount = segmentList.Count,
            TotalSegments = segments.Length,
            Segments = segmentList
        }));
    }

    private Task<ExecutionResult> GetSketchSegmentInfoAsync(IDictionary<string, object?> parameters)
    {
        if (!TryGetActiveSketch(out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var segmentId = GetIntParam(parameters, "SegmentId", -1);
        if (segmentId < 0)
        {
            return Task.FromResult(ExecutionResult.Failure("SegmentId parameter is required"));
        }

        var segmentsObject = activeSketch!.GetSketchSegments();
        if (segmentsObject == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No segments in active sketch"));
        }

        var segments = segmentsObject.ToObjectArraySafe();
        if (segments == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to retrieve sketch segments"));
        }

        SwSketchSegment? targetSegment = null;
        foreach (var segmentObject in segments)
        {
            if (segmentObject is not SwSketchSegment segment)
            {
                continue;
            }

            var ids = (int[])segment.GetID();
            if (ids[0] == segmentId)
            {
                targetSegment = segment;
                break;
            }
        }

        if (targetSegment == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Segment with ID {segmentId} not found"));
        }

        var typeName = SketchInspectionSegmentSupport.GetSegmentTypeName(targetSegment);
        var info = new Dictionary<string, object?>
        {
            ["Id"] = (int[])targetSegment.GetID(),
            ["Type"] = typeName,
            ["ConstructionGeometry"] = targetSegment.ConstructionGeometry,
            ["Length"] = MetersToMm(targetSegment.GetLength())
        };

        if (targetSegment is ISketchLine line)
        {
            var startPoint = line.GetStartPoint2() as ISketchPoint;
            var endPoint = line.GetEndPoint2() as ISketchPoint;
            if (startPoint != null && endPoint != null)
            {
                info["StartPoint"] = new { X = MetersToMm(startPoint.X), Y = MetersToMm(startPoint.Y), Z = MetersToMm(startPoint.Z) };
                info["EndPoint"] = new { X = MetersToMm(endPoint.X), Y = MetersToMm(endPoint.Y), Z = MetersToMm(endPoint.Z) };
            }
        }
        else if (targetSegment is ISketchArc arc)
        {
            var centerPoint = arc.GetCenterPoint2() as ISketchPoint;
            var startPoint = arc.GetStartPoint2() as ISketchPoint;
            var endPoint = arc.GetEndPoint2() as ISketchPoint;
            if (centerPoint != null && startPoint != null && endPoint != null)
            {
                info["CenterPoint"] = new { X = MetersToMm(centerPoint.X), Y = MetersToMm(centerPoint.Y), Z = MetersToMm(centerPoint.Z) };
                info["StartPoint"] = new { X = MetersToMm(startPoint.X), Y = MetersToMm(startPoint.Y), Z = MetersToMm(startPoint.Z) };
                info["EndPoint"] = new { X = MetersToMm(endPoint.X), Y = MetersToMm(endPoint.Y), Z = MetersToMm(endPoint.Z) };
            }

            info["Radius"] = MetersToMm(arc.GetRadius());
        }
        else if (targetSegment is ISketchPoint point)
        {
            info["Coordinates"] = new { X = MetersToMm(point.X), Y = MetersToMm(point.Y), Z = MetersToMm(point.Z) };
        }

        return Task.FromResult(ExecutionResult.SuccessResult(info));
    }

    private bool TryGetActiveSketch(out Sketch? activeSketch, out string? errorMessage)
    {
        errorMessage = null;
        activeSketch = null;

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

        activeSketch = model.SketchManager.ActiveSketch as Sketch;
        if (activeSketch == null)
        {
            errorMessage = "No active sketch. Use Sketch.CreateSketch first.";
            return false;
        }

        return true;
    }
}
