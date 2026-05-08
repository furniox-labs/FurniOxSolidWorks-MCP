using System;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Drawing;

internal static class DrawingSheetSupport
{
    internal static List<DrawingSheet> Extract(DrawingDoc drawing, ILogger logger)
    {
        var sheets = new List<DrawingSheet>();

        try
        {
            var sheetNamesObject = drawing.GetSheetNames();
            if (sheetNamesObject == null)
            {
                return sheets;
            }

            var sheetNames = sheetNamesObject as string[] ??
                sheetNamesObject.ToObjectArraySafe()?.Cast<string>().ToArray();
            if (sheetNames == null)
            {
                return sheets;
            }

            var currentSheetName = (drawing.GetCurrentSheet() as Sheet)?.GetName() ?? string.Empty;

            foreach (var sheetName in sheetNames)
            {
                var sheet = drawing.Sheet[sheetName] as Sheet;
                if (sheet == null)
                {
                    continue;
                }

                var properties = sheet.GetProperties().ToDoubleArraySafe();
                if (properties == null)
                {
                    continue;
                }

                var scaleNumerator = properties.Length > 2 ? properties[2] : 1;
                var scaleDenominator = properties.Length > 3 ? properties[3] : 1;
                var widthMeters = properties.Length > 5 ? properties[5] : 0;
                var heightMeters = properties.Length > 6 ? properties[6] : 0;
                var paperSizeCode = properties.Length > 0 ? (int)properties[0] : 0;

                var views = sheet.GetViews().ToObjectArraySafe();
                var viewCount = views?.Length > 0 ? views.Length - 1 : 0;

                sheets.Add(new DrawingSheet
                {
                    Name = sheetName,
                    Scale = scaleDenominator > 0 ? scaleNumerator / scaleDenominator : 1.0,
                    SheetFormat = sheet.GetTemplateName() ?? string.Empty,
                    PaperSize = new PaperSize
                    {
                        StandardSize = AnalysisHelpers.GetPaperSizeName(paperSizeCode),
                        Width = MetersToMm(widthMeters),
                        Height = MetersToMm(heightMeters)
                    },
                    IsActive = sheetName == currentSheetName,
                    ViewCount = viewCount,
                    CustomProperties = new Dictionary<string, string>()
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract sheet information");
        }

        return sheets;
    }

    private static double MetersToMm(double meters) => meters * 1000.0;
}
