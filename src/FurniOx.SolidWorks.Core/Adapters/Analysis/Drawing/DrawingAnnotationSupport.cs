using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Drawing;

internal static class DrawingAnnotationSupport
{
    internal static List<DrawingAnnotation> Extract(DrawingDoc drawing, ILogger logger)
    {
        var annotations = new List<DrawingAnnotation>();

        try
        {
            var sheets = drawing.GetViews().ToObjectArraySafe();
            if (sheets == null)
            {
                return annotations;
            }

            foreach (var sheetViewsObject in sheets)
            {
                var sheetViews = sheetViewsObject.ToObjectArraySafe();
                if (sheetViews == null)
                {
                    continue;
                }

                for (var index = 1; index < sheetViews.Length; index++)
                {
                    var view = sheetViews[index] as View;
                    if (view == null)
                    {
                        continue;
                    }

                    var viewName = view.GetName2();

                    var displayDimension = view.GetFirstDisplayDimension5();
                    while (displayDimension != null)
                    {
                        try
                        {
                            var annotation = displayDimension.GetAnnotation() as Annotation;
                            var dimension = displayDimension.GetDimension2(0);
                            if (annotation != null && dimension != null)
                            {
                                var position = annotation.GetPosition() as double[];
                                annotations.Add(new DrawingAnnotation
                                {
                                    Type = "Dimension",
                                    TypeCode = displayDimension.Type2,
                                    Text = dimension.FullName ?? string.Empty,
                                    ViewName = viewName,
                                    Position = new Point2D
                                    {
                                        X = position != null && position.Length >= 2 ? MetersToMm(position[0]) : 0,
                                        Y = position != null && position.Length >= 2 ? MetersToMm(position[1]) : 0
                                    },
                                    DimensionProps = new DimensionProperties
                                    {
                                        DimensionType = AnalysisHelpers.GetDimensionTypeName(displayDimension.Type2),
                                        Value = 0,
                                        Units = "mm"
                                    }
                                });
                            }
                        }
                        catch
                        {
                        }

                        displayDimension = displayDimension.GetNext5();
                    }

                    var notes = view.GetNotes().ToObjectArraySafe();
                    if (notes == null)
                    {
                        continue;
                    }

                    foreach (var noteObject in notes)
                    {
                        if (noteObject is not Note note)
                        {
                            continue;
                        }

                        try
                        {
                            var annotation = note.GetAnnotation() as Annotation;
                            if (annotation == null)
                            {
                                continue;
                            }

                            var position = annotation.GetPosition() as double[];
                            var text = note.GetText() ?? string.Empty;
                            var isBalloon = note.IsBomBalloon();

                            annotations.Add(new DrawingAnnotation
                            {
                                Type = isBalloon ? "Balloon" : "Note",
                                TypeCode = isBalloon ? 17 : 11,
                                Text = text,
                                ViewName = viewName,
                                Position = new Point2D
                                {
                                    X = position != null && position.Length >= 2 ? MetersToMm(position[0]) : 0,
                                    Y = position != null && position.Length >= 2 ? MetersToMm(position[1]) : 0
                                },
                                BalloonProps = isBalloon
                                    ? new BalloonProperties
                                    {
                                        ItemNumber = int.TryParse(text, out var itemNumber) ? itemNumber : 0,
                                        ItemText = text,
                                        BalloonStyle = "Circular"
                                    }
                                    : null
                            });
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract annotations");
        }

        return annotations;
    }

    private static double MetersToMm(double meters) => meters * 1000.0;
}
