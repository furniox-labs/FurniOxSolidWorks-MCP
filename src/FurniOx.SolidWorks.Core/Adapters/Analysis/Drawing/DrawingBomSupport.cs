using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Drawing;

internal static class DrawingBomSupport
{
    internal static List<Shared.Models.BomTable> Extract(DrawingDoc drawing, ILogger logger)
    {
        var bomTables = new List<Shared.Models.BomTable>();

        try
        {
            var view = (drawing.GetFirstView() as View)?.GetNextView() as View;
            while (view != null)
            {
                var tables = view.GetTableAnnotations().ToObjectArraySafe();
                if (tables != null)
                {
                    foreach (var tableObject in tables)
                    {
                        if (tableObject is not ITableAnnotation tableAnnotation ||
                            tableAnnotation.Type != (int)swTableAnnotationType_e.swTableAnnotation_BillOfMaterials ||
                            tableObject is not BomTableAnnotation bomTableAnnotation ||
                            tableObject is not TableAnnotation table)
                        {
                            continue;
                        }

                        var annotation = tableAnnotation.GetAnnotation() as Annotation;
                        var position = annotation?.GetPosition() as double[];
                        var rows = tableAnnotation.RowCount;
                        var columns = tableAnnotation.ColumnCount;
                        var headers = new List<string>();

                        for (var column = 0; column < columns; column++)
                        {
                            headers.Add(table.Text[0, column] ?? string.Empty);
                        }

                        var bomRows = new List<BomRow>();
                        for (var row = 1; row < rows; row++)
                        {
                            var rowData = new Dictionary<string, string>();
                            for (var column = 0; column < columns; column++)
                            {
                                var header = column < headers.Count ? headers[column] : $"Column{column}";
                                rowData[header] = table.Text[row, column] ?? string.Empty;
                            }

                            var itemNumberText = rowData.ContainsKey("ITEM NO.") ? rowData["ITEM NO."] : string.Empty;
                            var quantityText = rowData.ContainsKey("QTY") ? rowData["QTY"] : "1";

                            bomRows.Add(new BomRow
                            {
                                RowIndex = row,
                                ItemNumber = int.TryParse(itemNumberText, out var itemNumber) ? itemNumber : row,
                                Quantity = int.TryParse(quantityText, out var quantity) ? quantity : 1,
                                PartNumber = rowData.ContainsKey("PART NUMBER") ? rowData["PART NUMBER"] : string.Empty,
                                Description = rowData.ContainsKey("DESCRIPTION") ? rowData["DESCRIPTION"] : string.Empty,
                                Columns = rowData
                            });
                        }

                        bomTables.Add(new Shared.Models.BomTable
                        {
                            TableName = tableAnnotation.Title ?? "BOM",
                            TableType = AnalysisHelpers.GetBomTypeName(bomTableAnnotation.BomFeature?.TableType ?? 0),
                            TypeCode = bomTableAnnotation.BomFeature?.TableType ?? 0,
                            SheetName = view.GetName2() ?? "Unknown",
                            Position = new Point2D
                            {
                                X = position != null && position.Length >= 2 ? MetersToMm(position[0]) : 0,
                                Y = position != null && position.Length >= 2 ? MetersToMm(position[1]) : 0
                            },
                            RowCount = rows,
                            ColumnCount = columns,
                            ColumnHeaders = headers,
                            Rows = bomRows
                        });
                    }
                }

                view = view.GetNextView() as View;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract BOM tables");
        }

        return bomTables;
    }

    private static double MetersToMm(double meters) => meters * 1000.0;
}
