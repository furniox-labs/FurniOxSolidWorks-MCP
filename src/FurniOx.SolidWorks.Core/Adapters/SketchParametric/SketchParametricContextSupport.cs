using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.SketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchParametric;

internal static class SketchParametricContextSupport
{
    internal static bool TryGetActiveSketch(
        SolidWorksConnection connection,
        out SldWorks? app,
        out ModelDoc2? model,
        out Sketch? activeSketch,
        out string? errorMessage)
    {
        app = connection.Application;
        if (app == null)
        {
            model = null;
            activeSketch = null;
            errorMessage = "Not connected to SolidWorks";
            return false;
        }

        model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            activeSketch = null;
            errorMessage = "No active document";
            return false;
        }

        activeSketch = (Sketch?)model.SketchManager.ActiveSketch;
        if (activeSketch == null)
        {
            errorMessage = "No active sketch. Use Sketch.CreateSketch first.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    internal static bool TryParseEntityIds(
        object? entityIdsValue,
        out int[] entityIds,
        out string? errorMessage)
    {
        switch (entityIdsValue)
        {
            case int[] directIds:
                entityIds = directIds;
                errorMessage = null;
                return true;

            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Array:
                entityIds = jsonElement.EnumerateArray().Select(element => element.GetInt32()).ToArray();
                errorMessage = null;
                return true;
        }

        var objectArray = entityIdsValue?.ToObjectArraySafe();
        if (objectArray == null)
        {
            entityIds = Array.Empty<int>();
            errorMessage = "EntityIds must be an array of integers";
            return false;
        }

        entityIds = objectArray.Select(value => Convert.ToInt32(value)).ToArray();
        errorMessage = null;
        return true;
    }

    internal static bool TrySelectConstraintEntities(
        ModelDoc2 model,
        Sketch activeSketch,
        IReadOnlyList<int> entityIds,
        out int selectedCount,
        out string? errorMessage)
    {
        model.ClearSelection2(true);

        var segments = activeSketch.GetSketchSegments()?.ToObjectArraySafe() ?? Array.Empty<object>();
        var points = activeSketch.GetSketchPoints2()?.ToObjectArraySafe() ?? Array.Empty<object>();
        selectedCount = 0;

        foreach (var entityId in entityIds)
        {
            var entityFound = false;

            foreach (var segmentObject in segments)
            {
                if (segmentObject is not SwSketchSegment segment)
                {
                    continue;
                }

                var ids = (int[])segment.GetID();
                if (ids.Length == 0 || ids[0] != entityId)
                {
                    continue;
                }

                if (!segment.Select4(true, null))
                {
                    errorMessage = $"Failed to select segment {entityId}";
                    return false;
                }

                entityFound = true;
                selectedCount++;
                break;
            }

            if (!entityFound && entityId < points.Length && points[entityId] is ISketchPoint point)
            {
                if (!point.Select4(true, null))
                {
                    errorMessage = $"Failed to select point {entityId}";
                    return false;
                }

                entityFound = true;
                selectedCount++;
            }

            if (!entityFound)
            {
                errorMessage = $"Entity with ID {entityId} not found";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    internal static bool TryGetSegments(
        Sketch activeSketch,
        out object[] segments,
        out string? errorMessage)
    {
        var segmentsObject = activeSketch.GetSketchSegments();
        if (segmentsObject == null)
        {
            segments = Array.Empty<object>();
            errorMessage = "No segments in active sketch";
            return false;
        }

        segments = segmentsObject.ToObjectArraySafe() ?? Array.Empty<object>();
        if (segments.Length == 0)
        {
            errorMessage = "Failed to retrieve sketch segments";
            return false;
        }

        errorMessage = null;
        return true;
    }

    internal static bool TrySelectDimensionSegments(
        IReadOnlyList<object> segments,
        IReadOnlyList<int> entityIds,
        out string? errorMessage)
    {
        foreach (var entityId in entityIds)
        {
            SwSketchSegment? targetSegment = null;
            foreach (var segmentObject in segments)
            {
                if (segmentObject is not SwSketchSegment segment)
                {
                    continue;
                }

                var ids = (int[])segment.GetID();
                if (ids.Length > 0 && ids[0] == entityId)
                {
                    targetSegment = segment;
                    break;
                }
            }

            if (targetSegment == null)
            {
                errorMessage = $"Entity with ID {entityId} not found";
                return false;
            }

            if (!targetSegment.Select4(true, null))
            {
                errorMessage = $"Failed to select entity {entityId}";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }
}
