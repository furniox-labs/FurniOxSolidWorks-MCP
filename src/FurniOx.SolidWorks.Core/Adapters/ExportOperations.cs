using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Handles all export-related operations (5 export routes, 3 methods)
/// </summary>
public class ExportOperations : OperationHandlerBase
{
    public ExportOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<ExportOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override async Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Export.ExportToSTEP" => await ExportModelAsync(parameters, cancellationToken),
            "Export.ExportToIGES" => await ExportModelAsync(parameters, cancellationToken),
            "Export.ExportToSTL" => await ExportModelAsync(parameters, cancellationToken),
            "Export.ExportToPDF" => await ExportToPDFAsync(parameters, cancellationToken),
            "Export.ExportToDXF" => await ExportToDXFAsync(parameters, cancellationToken),
            _ => ExecutionResult.Failure($"Unknown export operation: {operation}")
        };
    }

    private Task<ExecutionResult> ExportModelAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {

        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract export path parameter
        if (!parameters.TryGetValue("Path", out var pathObj) || pathObj is not string exportPath)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Path' parameter"));
        }

        if (!IsPathSafe(exportPath, out var pathError))
        {
            return Task.FromResult(ExecutionResult.Failure($"Invalid export path: {pathError}"));
        }

        // Get document title for activation
        var docTitle = model.GetTitle();

        // CRITICAL: Activate document before export to prevent memory leaks
        int activationErrors = 0;
        app.ActivateDoc3(docTitle, false, 0, ref activationErrors);

        if (activationErrors != 0)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to activate document before export (Error: {activationErrors})"));
        }

        // Export via SaveAs3 - format determined by file extension
        int errors = 0, warnings = 0;

        bool result = model.Extension.SaveAs3(
            exportPath,
            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            null,  // IAdvancedSaveAsOptions
            null,  // ExportData
            ref errors,
            ref warnings);

        if (!result || errors != 0)
        {
            return Task.FromResult(ExecutionResult.Failure($"Export failed to {exportPath} (Errors: {errors}, Warnings: {warnings})"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Exported = true,
            Path = exportPath,
            Format = Path.GetExtension(exportPath).ToUpperInvariant(),
            Errors = errors,
            Warnings = warnings
        }));

    }

    private Task<ExecutionResult> ExportToPDFAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {

        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract export path parameter
        if (!parameters.TryGetValue("Path", out var pathObj) || pathObj is not string exportPath)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Path' parameter"));
        }

        if (!IsPathSafe(exportPath, out var pathError))
        {
            return Task.FromResult(ExecutionResult.Failure($"Invalid export path: {pathError}"));
        }

        // Get PDF export data object
        var pdfData = (IExportPdfData?)app.GetExportFileData((int)swExportDataFileType_e.swExportPdfData);
        if (pdfData == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get PDF export data object"));
        }

        // Configure PDF settings
        pdfData.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);
        pdfData.ViewPdfAfterSaving = false;

        // Export using SaveAs (not SaveAs3) with PDF data
        int errors = 0, warnings = 0;
        bool result = model.Extension.SaveAs(
            exportPath,
            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            pdfData,
            ref errors,
            ref warnings);

        if (!result || errors != 0)
        {
            return Task.FromResult(ExecutionResult.Failure($"PDF export failed to {exportPath} (Errors: {errors}, Warnings: {warnings})"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Exported = true,
            Path = exportPath,
            Format = "PDF",
            Errors = errors,
            Warnings = warnings
        }));

    }

    private Task<ExecutionResult> ExportToDXFAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {

        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract export path parameter
        if (!parameters.TryGetValue("Path", out var pathObj) || pathObj is not string exportPath)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Path' parameter"));
        }

        if (!IsPathSafe(exportPath, out var pathError))
        {
            return Task.FromResult(ExecutionResult.Failure($"Invalid export path: {pathError}"));
        }

        // DXF/DWG export is primarily for Part documents
        var partDoc = model as IPartDoc;
        if (partDoc == null)
        {
            return Task.FromResult(ExecutionResult.Failure("DXF/DWG export requires a Part document"));
        }

        // Use ExportToDWG2 for proper DXF/DWG export with sheet metal support
        // Sheet metal options: 1=flat pattern, 4=bend lines, 8=sketches (13=all)
        int sheetMetalOptions = GetIntParam(parameters, "SheetMetalOptions", 13);
        bool includeFlatPattern = GetBoolParam(parameters, "IncludeFlatPattern", true);

        string? modelPath = model.GetPathName();
        if (string.IsNullOrEmpty(modelPath))
        {
            modelPath = exportPath; // Use export path if model not saved
        }

        // ExportToDWG2: (FilePath, ModelPath, ExportType, IncludeFlatPattern, ViewToExport,
        //                ExportAllSheets, ExportAllViews, SheetMetalOptions, DwgExportData)
        // ExportType: 1=DWG, 0=DXF (determined by file extension)
        int exportType = exportPath.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        bool result = partDoc.ExportToDWG2(
            exportPath,           // File path
            modelPath,            // Model path name
            exportType,           // 0=DXF, 1=DWG
            includeFlatPattern,   // Include flat pattern
            null,                 // View to export (null = all)
            false,                // Export all sheets
            false,                // Export all views
            sheetMetalOptions,    // Sheet metal options (bitmask)
            null);                // DWG export data

        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"DXF/DWG export failed to {exportPath}"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Exported = true,
            Path = exportPath,
            Format = Path.GetExtension(exportPath).ToUpperInvariant(),
            ExportType = exportType == 1 ? "DWG" : "DXF",
            SheetMetalOptions = sheetMetalOptions,
            IncludedFlatPattern = includeFlatPattern
        }));

    }
}
