using System;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

internal static class AnalysisTransformSupport
{
    public static double MmToMeters(double millimeters) => millimeters / 1000.0;

    public static double MetersToMm(double meters) => meters * 1000.0;

    public static AssemblyTransform? ExtractTransform(Component2 component, ILogger? logger)
    {
        try
        {
            var transform = (MathTransform?)component.Transform2;
            if (transform == null)
            {
                return null;
            }

            var arrayData = (double[]?)transform.ArrayData;
            if (arrayData == null || arrayData.Length < 16)
            {
                return null;
            }

            return new AssemblyTransform
            {
                TransformMatrix = arrayData,
                Translation = new Point3D
                {
                    X = MetersToMm(arrayData[9]),
                    Y = MetersToMm(arrayData[10]),
                    Z = MetersToMm(arrayData[11])
                },
                Rotation =
                [
                    arrayData[0], arrayData[1], arrayData[2],
                    arrayData[3], arrayData[4], arrayData[5],
                    arrayData[6], arrayData[7], arrayData[8]
                ]
            };
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to extract component transform");
            return null;
        }
    }
}
