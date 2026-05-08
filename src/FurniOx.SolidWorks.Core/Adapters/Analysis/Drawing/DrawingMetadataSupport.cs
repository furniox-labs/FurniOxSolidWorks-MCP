using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Drawing;

internal static class DrawingMetadataSupport
{
    internal static DrawingMetadata Extract(ModelDoc2 model, DrawingDoc drawing)
    {
        var activeConfiguration = model.ConfigurationManager.ActiveConfiguration as Configuration;
        _ = activeConfiguration;

        var sheetCount = drawing.GetSheetCount();
        var currentSheet = drawing.GetCurrentSheet() as Sheet;
        var activeSheetName = currentSheet?.GetName() ?? "Unknown";

        var unitSystem = model.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swUnitSystem);
        var units = unitSystem switch
        {
            1 => "in",
            2 => "m",
            3 => "mm",
            4 => "cm",
            _ => "mm"
        };

        var standard = model.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDetailingDimensionStandard);
        var drawingStandard = standard switch
        {
            0 => "ANSI",
            1 => "ISO",
            2 => "DIN",
            3 => "JIS",
            4 => "BSI",
            5 => "GOST",
            6 => "GB",
            _ => "Unknown"
        };

        var projectionType = "Third Angle";
        if (currentSheet != null)
        {
            var properties = currentSheet.GetProperties().ToDoubleArraySafe();
            if (properties != null && properties.Length > 4)
            {
                projectionType = properties[4] != 0 ? "First Angle" : "Third Angle";
            }
        }

        var totalViews = 0;
        try
        {
            var sheets = drawing.GetViews().ToObjectArraySafe();
            if (sheets != null)
            {
                foreach (var sheetViewsObject in sheets)
                {
                    var sheetViews = sheetViewsObject.ToObjectArraySafe();
                    if (sheetViews != null)
                    {
                        totalViews += sheetViews.Length - 1;
                    }
                }
            }
        }
        catch
        {
        }

        return new DrawingMetadata
        {
            Name = model.GetTitle(),
            SheetCount = sheetCount,
            ViewCount = totalViews,
            ActiveSheet = activeSheetName,
            DrawingStandard = drawingStandard,
            Units = units,
            ProjectionType = projectionType
        };
    }
}
