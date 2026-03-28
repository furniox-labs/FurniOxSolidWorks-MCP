using System;
using System.Linq;
using FurniOx.SolidWorks.Core.Extensions;

namespace FurniOx.SolidWorks.Core.Adapters.SketchAdvanced;

internal static class SketchAdvancedContextSupport
{
    internal static double[]? ParsePointArray(object? pointsValue)
    {
        if (pointsValue is double[] points)
        {
            return points;
        }

        return pointsValue
            .ToObjectArraySafe()?
            .Select(value => Convert.ToDouble(value) / 1000.0)
            .ToArray();
    }
}
