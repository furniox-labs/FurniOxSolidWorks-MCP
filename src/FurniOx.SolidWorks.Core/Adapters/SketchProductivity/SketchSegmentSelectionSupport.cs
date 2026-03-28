using System;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using SolidWorks.Interop.sldworks;
using SwSketchSegment = SolidWorks.Interop.sldworks.SketchSegment;

namespace FurniOx.SolidWorks.Core.Adapters.SketchProductivity;

internal static class SketchSegmentSelectionSupport
{
    internal static bool TryGetActiveSketch(
        SolidWorksConnection connection,
        out ModelDoc2? model,
        out SketchManager? sketchManager,
        out Sketch? activeSketch,
        out string? errorMessage)
    {
        errorMessage = null;
        model = null;
        sketchManager = null;
        activeSketch = null;

        var app = connection.Application;
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
        activeSketch = sketchManager.ActiveSketch as Sketch;
        if (activeSketch == null)
        {
            errorMessage = "No active sketch";
            return false;
        }

        return true;
    }

    internal static int[] ParseEntityIds(object? entityIdsValue)
    {
        if (entityIdsValue is int[] ids)
        {
            return ids;
        }

        return entityIdsValue
            .ToObjectArraySafe()?
            .Select(value => Convert.ToInt32(value))
            .ToArray() ?? Array.Empty<int>();
    }

    internal static bool TryGetSegments(Sketch activeSketch, out object[]? segments, out string? errorMessage)
    {
        errorMessage = null;
        segments = activeSketch.GetSketchSegments().ToObjectArraySafe();
        if (segments == null)
        {
            errorMessage = "Failed to read sketch segments";
            return false;
        }

        return true;
    }

    internal static SwSketchSegment? FindSegment(object[] segments, int entityId)
    {
        foreach (var segmentObject in segments)
        {
            var segment = segmentObject as SwSketchSegment;
            if (segment != null && ((int[])segment.GetID())[0] == entityId)
            {
                return segment;
            }
        }

        return null;
    }

    internal static bool TrySelectSegments(
        ModelDoc2 model,
        object[] segments,
        IEnumerable<int> entityIds,
        out int selectedCount,
        out string? errorMessage)
    {
        errorMessage = null;
        selectedCount = 0;

        foreach (var entityId in entityIds)
        {
            var segment = FindSegment(segments, entityId);
            if (segment == null)
            {
                errorMessage = $"Entity ID {entityId} not found";
                return false;
            }

            if (!segment.Select4(true, null))
            {
                errorMessage = $"Failed to select entity {entityId}";
                return false;
            }

            selectedCount++;
        }

        return true;
    }
}
