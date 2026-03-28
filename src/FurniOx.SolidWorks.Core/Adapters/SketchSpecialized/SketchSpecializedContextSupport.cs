using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchSpecialized;

internal static class SketchSpecializedContextSupport
{
    internal static bool TryGetActiveSketch(
        SolidWorksConnection connection,
        out SldWorks? app,
        out ModelDoc2? model,
        out SketchManager? sketchManager,
        out Sketch? activeSketch,
        out string? errorMessage)
    {
        app = connection.Application;
        if (app == null)
        {
            model = null;
            sketchManager = null;
            activeSketch = null;
            errorMessage = "Not connected to SolidWorks";
            return false;
        }

        model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            sketchManager = null;
            activeSketch = null;
            errorMessage = "No active document";
            return false;
        }

        sketchManager = model.SketchManager;
        activeSketch = (Sketch?)sketchManager.ActiveSketch;
        if (activeSketch == null)
        {
            errorMessage = "No active sketch";
            return false;
        }

        errorMessage = null;
        return true;
    }

    internal static bool TryGetRelationTypeName(int typeCode, out string relationTypeName)
    {
        relationTypeName = typeCode switch
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
            15 => "Fixed",
            16 => "EqualCurvature",
            17 => "Pierce",
            18 => "AlongZ",
            _ => $"Unknown({typeCode})"
        };

        return true;
    }

    internal static object[] GetObjectArrayOrEmpty(object? value)
    {
        return value.ToObjectArraySafe() ?? [];
    }
}
