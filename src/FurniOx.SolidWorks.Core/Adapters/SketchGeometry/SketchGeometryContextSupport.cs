using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchGeometry;

internal static class SketchGeometryContextSupport
{
    internal static bool TryGetModel(
        SolidWorksConnection connection,
        out ModelDoc2? model,
        out string? errorMessage)
    {
        model = null;
        errorMessage = null;

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

        return true;
    }

    internal static double[]? ParsePointArray(object? pointsValue)
    {
        if (pointsValue is double[] points)
        {
            return points;
        }

        return pointsValue
            .ToObjectArraySafe()?
            .Select(value => value is double number ? number : 0.0)
            .ToArray();
    }

    internal static IFeature? FindFeatureByName(ModelDoc2 model, string featureName)
    {
        var feature = model.FirstFeature() as IFeature;
        while (feature != null)
        {
            if (feature.Name == featureName)
            {
                return feature;
            }

            var subFeature = feature.GetFirstSubFeature() as IFeature;
            while (subFeature != null)
            {
                if (subFeature.Name == featureName)
                {
                    return subFeature;
                }

                subFeature = subFeature.GetNextSubFeature() as IFeature;
            }

            feature = feature.GetNextFeature() as IFeature;
        }

        return null;
    }
}
