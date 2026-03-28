using System;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.ISketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchInspection;

internal static class SketchInspectionGeometrySupport
{
    internal static List<Shared.Models.SketchSegment> ExtractSegments(Sketch sketch, bool includeConstruction)
    {
        var segments = new List<Shared.Models.SketchSegment>();
        var segmentArray = sketch.GetSketchSegments().ToObjectArraySafe();
        if (segmentArray == null)
        {
            return segments;
        }

        for (var index = 0; index < segmentArray.Length; index++)
        {
            if (segmentArray[index] is not SwSketchSegment segment)
            {
                continue;
            }

            if (!includeConstruction && segment.ConstructionGeometry)
            {
                continue;
            }

            var segmentType = SketchInspectionSegmentSupport.GetSegmentTypeName(segment);
            var geometry = ExtractSegmentGeometry(segment, segmentType);

            segments.Add(new Shared.Models.SketchSegment
            {
                Index = index,
                TypeCode = SketchInspectionSegmentSupport.GetSegmentTypeCode(segment),
                Type = segmentType,
                Id = SketchInspectionSegmentSupport.GetSegmentId(segment),
                IsConstruction = segment.ConstructionGeometry,
                Geometry = geometry
            });
        }

        return segments;
    }

    internal static SketchSegmentGeometry ExtractSegmentGeometry(SwSketchSegment segment, string type)
    {
        var geometry = new SketchSegmentGeometry();

        switch (type)
        {
            case "Line":
                if (segment is ISketchLine line)
                {
                    var startPoint = line.GetStartPoint2() as ISketchPoint;
                    var endPoint = line.GetEndPoint2() as ISketchPoint;

                    if (startPoint != null && endPoint != null)
                    {
                        geometry = geometry with
                        {
                            StartPoint = new Point3D { X = MetersToMm(startPoint.X), Y = MetersToMm(startPoint.Y), Z = MetersToMm(startPoint.Z) },
                            EndPoint = new Point3D { X = MetersToMm(endPoint.X), Y = MetersToMm(endPoint.Y), Z = MetersToMm(endPoint.Z) },
                            Length = MetersToMm(segment.GetLength())
                        };
                    }
                }
                break;

            case "Arc":
            case "Circle":
                if (segment is ISketchArc arc)
                {
                    var centerPoint = arc.GetCenterPoint2() as ISketchPoint;
                    var startPoint = arc.GetStartPoint2() as ISketchPoint;
                    var endPoint = arc.GetEndPoint2() as ISketchPoint;
                    var radius = arc.GetRadius();
                    var lengthMeters = segment.GetLength();

                    var isFullCircle = type == "Circle" || SketchInspectionSegmentSupport.IsFullCircleSegment(segment, arc);
                    double? sweepAngleRadAbs = null;
                    if (radius > 0 && lengthMeters > 0)
                    {
                        sweepAngleRadAbs = lengthMeters / radius;
                    }

                    if (centerPoint != null && startPoint != null && endPoint != null)
                    {
                        var center = new[] { centerPoint.X, centerPoint.Y, centerPoint.Z };
                        var start = new[] { startPoint.X, startPoint.Y, startPoint.Z };
                        var end = new[] { endPoint.X, endPoint.Y, endPoint.Z };

                        var directionSignRadians = CalculateArcSweepAngle(center, start, end);
                        var direction = directionSignRadians >= 0 ? "CCW" : "CW";
                        var sweepRadians = sweepAngleRadAbs ?? Math.Abs(directionSignRadians);
                        if (isFullCircle)
                        {
                            sweepRadians = 2 * Math.PI;
                        }

                        geometry = geometry with
                        {
                            CenterPoint = new Point3D { X = MetersToMm(centerPoint.X), Y = MetersToMm(centerPoint.Y), Z = MetersToMm(centerPoint.Z) },
                            StartPoint = new Point3D { X = MetersToMm(startPoint.X), Y = MetersToMm(startPoint.Y), Z = MetersToMm(startPoint.Z) },
                            EndPoint = new Point3D { X = MetersToMm(endPoint.X), Y = MetersToMm(endPoint.Y), Z = MetersToMm(endPoint.Z) },
                            Radius = MetersToMm(radius),
                            IsFullCircle = isFullCircle,
                            SweepAngle = sweepRadians * (180.0 / Math.PI),
                            SweepAngleRadians = sweepRadians,
                            Direction = direction,
                            Length = MetersToMm(segment.GetLength())
                        };
                    }
                }
                break;

            case "Ellipse":
                if (segment is ISketchEllipse ellipse)
                {
                    var centerPoint = ellipse.GetCenterPoint2() as ISketchPoint;
                    var majorPoint = ellipse.GetMajorPoint2() as ISketchPoint;
                    var minorPoint = ellipse.GetMinorPoint2() as ISketchPoint;

                    if (centerPoint != null && majorPoint != null && minorPoint != null)
                    {
                        var majorRadius = Math.Sqrt(
                            Math.Pow(majorPoint.X - centerPoint.X, 2) +
                            Math.Pow(majorPoint.Y - centerPoint.Y, 2) +
                            Math.Pow(majorPoint.Z - centerPoint.Z, 2));

                        var minorRadius = Math.Sqrt(
                            Math.Pow(minorPoint.X - centerPoint.X, 2) +
                            Math.Pow(minorPoint.Y - centerPoint.Y, 2) +
                            Math.Pow(minorPoint.Z - centerPoint.Z, 2));

                        geometry = geometry with
                        {
                            CenterPoint = new Point3D { X = MetersToMm(centerPoint.X), Y = MetersToMm(centerPoint.Y), Z = MetersToMm(centerPoint.Z) },
                            MajorPoint = new Point3D { X = MetersToMm(majorPoint.X), Y = MetersToMm(majorPoint.Y), Z = MetersToMm(majorPoint.Z) },
                            MinorPoint = new Point3D { X = MetersToMm(minorPoint.X), Y = MetersToMm(minorPoint.Y), Z = MetersToMm(minorPoint.Z) },
                            MajorRadius = MetersToMm(majorRadius),
                            MinorRadius = MetersToMm(minorRadius),
                            Length = MetersToMm(segment.GetLength())
                        };
                    }
                }
                break;

            default:
                geometry = geometry with { Length = MetersToMm(segment.GetLength()) };
                break;
        }

        return geometry;
    }

    internal static SketchConnectivity CalculateConnectivity(
        IReadOnlyList<Shared.Models.SketchSegment> segments,
        double toleranceMm,
        bool includeConstructionGeometry)
    {
        toleranceMm = toleranceMm <= 0 ? 0.01 : toleranceMm;
        var toleranceSquared = toleranceMm * toleranceMm;

        var consideredSegments = includeConstructionGeometry
            ? segments
            : segments.Where(segment => !segment.IsConstruction).ToList();

        var endpoints = new List<(int SegmentIndex, int[]? SegmentId, string SegmentType, string Endpoint, Point3D Point)>();
        foreach (var segment in consideredSegments)
        {
            if (segment.Geometry.IsFullCircle == true)
            {
                continue;
            }

            if (segment.Geometry.StartPoint is { } startPoint)
            {
                endpoints.Add((segment.Index, segment.Id, segment.Type, "start", startPoint));
            }

            if (segment.Geometry.EndPoint is { } endPoint)
            {
                endpoints.Add((segment.Index, segment.Id, segment.Type, "end", endPoint));
            }
        }

        var openEndpoints = new List<SketchOpenEndpoint>();

        for (var index = 0; index < endpoints.Count; index++)
        {
            var current = endpoints[index];
            var hasPartner = false;
            var nearestDistanceSquared = double.PositiveInfinity;

            for (var innerIndex = 0; innerIndex < endpoints.Count; innerIndex++)
            {
                if (index == innerIndex)
                {
                    continue;
                }

                var distanceSquared = DistanceSquared(current.Point, endpoints[innerIndex].Point);
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                }

                if (distanceSquared <= toleranceSquared)
                {
                    hasPartner = true;
                    break;
                }
            }

            if (!hasPartner)
            {
                openEndpoints.Add(new SketchOpenEndpoint
                {
                    SegmentIndex = current.SegmentIndex,
                    SegmentId = current.SegmentId,
                    SegmentType = current.SegmentType,
                    Endpoint = current.Endpoint,
                    Point = current.Point,
                    NearestEndpointDistanceMm = double.IsFinite(nearestDistanceSquared) ? Math.Sqrt(nearestDistanceSquared) : null
                });
            }
        }

        return new SketchConnectivity
        {
            ToleranceMm = toleranceMm,
            IncludeConstructionGeometry = includeConstructionGeometry,
            ConsideredSegmentCount = consideredSegments.Count,
            EndpointCount = endpoints.Count,
            OpenEndpointCount = openEndpoints.Count,
            OpenEndpoints = openEndpoints
        };
    }

    private static double CalculateArcSweepAngle(double[] center, double[] start, double[] end)
    {
        var v1x = start[0] - center[0];
        var v1y = start[1] - center[1];
        var v2x = end[0] - center[0];
        var v2y = end[1] - center[1];

        var angle1 = Math.Atan2(v1y, v1x);
        var angle2 = Math.Atan2(v2y, v2x);
        var sweep = angle2 - angle1;

        if (sweep > Math.PI)
        {
            sweep -= 2 * Math.PI;
        }

        if (sweep < -Math.PI)
        {
            sweep += 2 * Math.PI;
        }

        return sweep;
    }

    private static double DistanceSquared(Point3D a, Point3D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static double MetersToMm(double meters) => meters * 1000.0;
}
