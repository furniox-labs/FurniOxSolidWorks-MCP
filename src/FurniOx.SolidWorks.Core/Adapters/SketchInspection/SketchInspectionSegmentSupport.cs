using System;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.ISketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchInspection;

internal static class SketchInspectionSegmentSupport
{
    internal static string GetSegmentTypeName(SwSketchSegment segment)
    {
        if (segment is ISketchLine) return "Line";
        if (segment is ISketchArc arc)
        {
            return IsFullCircleSegment(segment, arc) ? "Circle" : "Arc";
        }

        if (segment is ISketchEllipse) return "Ellipse";
        if (segment is ISketchSpline) return "Spline";
        if (segment is ISketchPoint) return "Point";
        return "SketchSegment";
    }

    internal static int GetSegmentTypeCode(SwSketchSegment segment)
    {
        if (segment is ISketchLine) return 0;
        if (segment is ISketchArc) return 1;
        if (segment is ISketchEllipse) return 2;
        if (segment is ISketchSpline) return 3;
        if (segment is ISketchPoint) return 4;
        return -1;
    }

    internal static int[]? GetSegmentId(SwSketchSegment segment)
    {
        try
        {
            return (int[])segment.GetID();
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsFullCircleSegment(SwSketchSegment segment, ISketchArc arc)
    {
        try
        {
            var radius = arc.GetRadius();
            if (radius <= 0) return false;

            var length = segment.GetLength();
            if (length <= 0) return false;

            var fullCircleLength = 2 * Math.PI * radius;
            if (fullCircleLength <= 0) return false;

            var ratio = length / fullCircleLength;
            return ratio >= 0.98;
        }
        catch
        {
            return false;
        }
    }
}
