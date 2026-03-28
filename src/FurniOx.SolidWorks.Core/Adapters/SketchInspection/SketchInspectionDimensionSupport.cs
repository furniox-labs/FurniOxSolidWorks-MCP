using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.SketchInspection;

internal static class SketchInspectionDimensionSupport
{
    internal static List<SketchDimension> ExtractDimensions(Sketch sketch, ModelDoc2 model, ILogger logger)
    {
        var dimensions = new List<SketchDimension>();
        var feature = (IFeature)sketch;

        var displayDimension = feature.GetFirstDisplayDimension() as DisplayDimension;
        while (displayDimension != null)
        {
            var dimension = displayDimension.GetDimension2(0) as Dimension;
            if (dimension != null)
            {
                var dimensionType = displayDimension.Type2;
                var valueInMeters = 0.0;

                try
                {
                    var valueResult = dimension.GetSystemValue3(
                        (int)swInConfigurationOpts_e.swThisConfiguration,
                        string.Empty);

                    if (valueResult != null)
                    {
                        if (valueResult is double singleValue)
                        {
                            valueInMeters = singleValue;
                        }
                        else
                        {
                            var values = valueResult.ToDoubleArraySafe();
                            if (values != null && values.Length > 0)
                            {
                                valueInMeters = values[0];
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to get dimension value for {DimName}", dimension.FullName);
                }

                dimensions.Add(new SketchDimension
                {
                    Name = dimension.FullName ?? "Unknown",
                    Value = MetersToMm(valueInMeters),
                    TypeCode = dimensionType,
                    Type = GetDimensionTypeName(dimensionType),
                    IsDriven = dimension.DrivenState == (int)swDimensionDrivenState_e.swDimensionDriven
                });
            }

            displayDimension = feature.GetNextDisplayDimension(displayDimension) as DisplayDimension;
        }

        return dimensions;
    }

    private static string GetDimensionTypeName(int typeCode)
    {
        return typeCode switch
        {
            1 => "Linear",
            2 => "Angular",
            3 => "Radial",
            4 => "Diameter",
            5 => "ArcLength",
            6 => "Ordinate",
            _ => $"Unknown({typeCode})"
        };
    }

    private static double MetersToMm(double meters) => meters * 1000.0;
}
