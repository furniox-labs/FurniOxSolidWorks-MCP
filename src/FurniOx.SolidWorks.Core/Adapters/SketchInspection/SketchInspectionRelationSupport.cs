using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SwSketchPoint = SolidWorks.Interop.sldworks.ISketchPoint;
using SwSketchRelation = SolidWorks.Interop.sldworks.ISketchRelation;
using SwSketchSegment = SolidWorks.Interop.sldworks.ISketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchInspection;

internal static class SketchInspectionRelationSupport
{
    internal static List<Shared.Models.SketchRelation> ExtractRelations(
        Sketch sketch,
        List<Shared.Models.SketchSegment> segments)
    {
        var relations = new List<Shared.Models.SketchRelation>();
        var relationManager = sketch.RelationManager;
        if (relationManager == null)
        {
            return relations;
        }

        var relationArray = relationManager.GetRelations(0).ToObjectArraySafe();
        if (relationArray == null)
        {
            return relations;
        }

        for (var index = 0; index < relationArray.Length; index++)
        {
            if (relationArray[index] is not SwSketchRelation relation)
            {
                continue;
            }

            var relationType = relation.GetRelationType();
            var entities = new List<RelationEntity>();
            var entityArray = relation.GetDefinitionEntities2().ToObjectArraySafe();
            if (entityArray != null)
            {
                foreach (var entityObject in entityArray)
                {
                    var entity = ExtractRelationEntity(entityObject, segments);
                    if (entity != null)
                    {
                        entities.Add(entity);
                    }
                }
            }

            relations.Add(new Shared.Models.SketchRelation
            {
                Index = index,
                TypeCode = relationType,
                Type = GetRelationTypeName(relationType),
                Suppressed = false,
                EntityCount = entities.Count,
                Entities = entities
            });
        }

        return relations;
    }

    private static RelationEntity? ExtractRelationEntity(object entity, List<Shared.Models.SketchSegment> segments)
    {
        if (entity is SwSketchSegment segment)
        {
            var segmentId = SketchInspectionSegmentSupport.GetSegmentId(segment);
            var matchingSegment = segments.FirstOrDefault(s =>
                s.Id != null &&
                segmentId != null &&
                s.Id[0] == segmentId[0] &&
                s.Id[1] == segmentId[1]);

            return new RelationEntity
            {
                Type = "SketchSegment",
                SegmentType = SketchInspectionSegmentSupport.GetSegmentTypeCode(segment),
                SegmentTypeName = SketchInspectionSegmentSupport.GetSegmentTypeName(segment),
                Id = segmentId,
                Index = matchingSegment?.Index
            };
        }

        if (entity is SwSketchPoint)
        {
            return new RelationEntity
            {
                Type = "SketchPoint"
            };
        }

        if (entity is RefPlane plane)
        {
            return new RelationEntity
            {
                Type = "ModelGeometry",
                ModelGeometryType = "Plane",
                ModelGeometryName = ((IFeature)plane).Name
            };
        }

        return null;
    }

    private static string GetRelationTypeName(int typeCode)
    {
        return typeCode switch
        {
            0 => "None",
            1 => "Horizontal",
            2 => "Vertical",
            3 => "Coincident",
            4 => "Tangent",
            5 => "Perpendicular",
            6 => "Parallel",
            7 => "Equal",
            8 => "Concentric",
            9 => "Midpoint",
            10 => "Symmetric",
            11 => "Intersection",
            12 => "Collinear",
            13 => "CoplanarPlanes",
            14 => "Coradial",
            _ => $"Unknown({typeCode})"
        };
    }
}
