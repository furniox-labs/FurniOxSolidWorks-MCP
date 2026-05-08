using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Drawing;

internal static class DrawingViewSupport
{
    internal static List<DrawingView> Extract(DrawingDoc drawing, ILogger logger)
    {
        var views = new List<DrawingView>();

        try
        {
            var sheets = drawing.GetViews().ToObjectArraySafe();
            if (sheets == null)
            {
                return views;
            }

            foreach (var sheetViewsObject in sheets)
            {
                var sheetViews = sheetViewsObject.ToObjectArraySafe();
                if (sheetViews == null || sheetViews.Length == 0)
                {
                    continue;
                }

                var sheetView = sheetViews[0] as View;
                var sheetName = sheetView?.GetName2() ?? "Unknown";

                for (var index = 1; index < sheetViews.Length; index++)
                {
                    var view = sheetViews[index] as View;
                    if (view == null)
                    {
                        continue;
                    }

                    var scale = view.ScaleRatio as double[];
                    var scaleValue = scale != null && scale.Length >= 2 && scale[1] > 0
                        ? scale[0] / scale[1]
                        : 1.0;

                    var position = view.Position as double[];
                    var xMm = position != null && position.Length >= 2 ? MetersToMm(position[0]) : 0;
                    var yMm = position != null && position.Length >= 2 ? MetersToMm(position[1]) : 0;

                    var modelPath = string.Empty;
                    var configuration = string.Empty;
                    var referencedDocument = view.ReferencedDocument as ModelDoc2;
                    if (referencedDocument != null)
                    {
                        modelPath = referencedDocument.GetPathName() ?? string.Empty;
                        configuration = view.ReferencedConfiguration?.ToString() ?? string.Empty;
                    }
                    else if (view.GetBaseView() is View baseView)
                    {
                        referencedDocument = baseView.ReferencedDocument as ModelDoc2;
                        if (referencedDocument != null)
                        {
                            modelPath = referencedDocument.GetPathName() ?? string.Empty;
                            configuration = baseView.ReferencedConfiguration?.ToString() ?? string.Empty;
                        }
                    }

                    views.Add(new DrawingView
                    {
                        Name = view.GetName2(),
                        Type = AnalysisHelpers.GetViewTypeName(view.Type),
                        TypeCode = view.Type,
                        SheetName = sheetName,
                        Scale = scaleValue,
                        Position = new Point2D { X = xMm, Y = yMm },
                        RotationAngle = 0,
                        ReferencedModelPath = modelPath,
                        ReferencedConfiguration = configuration,
                        ParentViewName = (view.GetBaseView() as View)?.GetName2()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract view information");
        }

        return views;
    }

    private static double MetersToMm(double meters) => meters * 1000.0;
}
