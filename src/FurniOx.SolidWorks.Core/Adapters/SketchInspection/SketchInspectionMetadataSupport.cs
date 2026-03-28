using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchInspection;

internal static class SketchInspectionMetadataSupport
{
    internal static SketchMetadata ExtractMetadata(Sketch sketch, ModelDoc2 model)
    {
        var feature = (IFeature)sketch;
        var featureName = feature?.Name ?? "Unknown";
        var is3D = sketch.Is3D();

        var entityType = 0;
        var refEntity = sketch.GetReferenceEntity(ref entityType);
        var referencePlane = "Unknown";
        if (refEntity is IRefPlane plane)
        {
            referencePlane = ((IFeature)plane).Name ?? "Unknown";
        }

        object segmentsObject = sketch.GetSketchSegments();
        object pointsObject = sketch.GetSketchPoints2();
        ISketchRelationManager? relationManager = sketch.RelationManager;
        object? relationsObject = relationManager?.GetRelations(0);

        var segmentCount = segmentsObject.SafeArrayCount();
        var pointCount = pointsObject.SafeArrayCount();
        var relationCount = relationsObject?.SafeArrayCount() ?? 0;

        var totalEntities = segmentCount + relationCount;
        var complexity = totalEntities switch
        {
            < 10 => "simple",
            < 30 => "medium",
            _ => "complex"
        };

        return new SketchMetadata
        {
            SketchName = featureName,
            FeatureName = featureName,
            Is3D = is3D,
            ReferencePlane = referencePlane,
            TotalSegments = segmentCount,
            TotalPoints = pointCount,
            TotalRelations = relationCount,
            TotalDimensions = 0,
            Complexity = complexity
        };
    }

    internal static List<Shared.Models.SketchPoint> ExtractPoints(Sketch sketch)
    {
        var points = new List<Shared.Models.SketchPoint>();
        var pointArray = sketch.GetSketchPoints2().ToObjectArraySafe();
        if (pointArray == null)
        {
            return points;
        }

        for (var index = 0; index < pointArray.Length; index++)
        {
            if (pointArray[index] is not ISketchPoint point)
            {
                continue;
            }

            points.Add(new Shared.Models.SketchPoint
            {
                Index = index,
                X = MetersToMm(point.X),
                Y = MetersToMm(point.Y),
                Z = MetersToMm(point.Z)
            });
        }

        return points;
    }

    internal static SketchStatistics CalculateStatistics(List<Shared.Models.SketchSegment> segments)
    {
        var totalLength = 0.0;
        var typeDistribution = new Dictionary<string, int>();
        var constructionCount = 0;
        var regularCount = 0;

        foreach (var segment in segments)
        {
            if (segment.Geometry.Length.HasValue)
            {
                totalLength += segment.Geometry.Length.Value;
            }

            if (!typeDistribution.ContainsKey(segment.Type))
            {
                typeDistribution[segment.Type] = 0;
            }

            typeDistribution[segment.Type]++;

            if (segment.IsConstruction)
            {
                constructionCount++;
            }
            else
            {
                regularCount++;
            }
        }

        return new SketchStatistics
        {
            TotalLength = totalLength,
            EntityTypeDistribution = typeDistribution,
            ConstructionEntities = constructionCount,
            RegularEntities = regularCount
        };
    }

    private static double MetersToMm(double meters) => meters * 1000.0;
}
