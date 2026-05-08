using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

internal static class DocumentSaveHelper
{
    public static bool SaveWithRenamedReferences(ModelDoc2 model, ILogger logger, out int errors, out int warnings)
        => SaveWithRenamedReferences(model, logger, out errors, out warnings, null, null);

    public static bool SaveWithRenamedReferences(
        ModelDoc2 model,
        ILogger logger,
        out int errors,
        out int warnings,
        IEnumerable<string>? searchFolders,
        SldWorks? app)
    {
        errors = 0;
        warnings = 0;

        using var suppressionScope = app != null ? new DialogSuppressionScope(app, logger) : null;

        int ConfigureRenamedReferences(ref object refsObj)
        {
            try
            {
                if (refsObj is IRenamedDocumentReferences refs)
                {
                    refs.UpdateWhereUsedReferences = true;
                    refs.IncludeFileLocations = true;

                    foreach (var folder in BuildSearchFolders(model, searchFolders))
                    {
                        try { refs.AddSearchFolder(folder); }
                        catch (Exception ex) { logger.LogDebug(ex, "Failed to add renamed-reference search folder {Folder}", folder); }
                    }

                    try { refs.Search(); }
                    catch (Exception ex) { logger.LogDebug(ex, "Renamed-reference search failed"); }

                    refs.CompletionAction = (int)swRenamedDocumentFinalAction_e.swRenamedDocumentFinalAction_Ok;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to configure RenamedDocumentReferences");
            }

            return 0;
        }

        DAssemblyDocEvents_RenamedDocumentNotifyEventHandler assemblyHandler = ConfigureRenamedReferences;
        DPartDocEvents_RenamedDocumentNotifyEventHandler partHandler = ConfigureRenamedReferences;

        var assemblyDoc = model as AssemblyDoc;
        var partDoc = model as PartDoc;
        if (assemblyDoc != null)
        {
            assemblyDoc.RenamedDocumentNotify += assemblyHandler;
        }

        if (partDoc != null)
        {
            partDoc.RenamedDocumentNotify += partHandler;
        }

        try
        {
            var extension = model.Extension;
            bool hasRenamedDocs = extension.HasRenamedDocuments();
            logger.LogDebug("HasRenamedDocuments: {HasRenamedDocs}", hasRenamedDocs);

            if (!hasRenamedDocs)
            {
                return model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
            }

            var currentPath = model.GetPathName();
            if (string.IsNullOrEmpty(currentPath))
            {
                logger.LogError("Cannot save renamed document - no path set");
                errors = (int)swFileSaveError_e.swGenericSaveError;
                return false;
            }

            var advancedOptions = (AdvancedSaveAsOptions?)extension.GetAdvancedSaveAsOptions(
                (int)swSaveWithReferencesOptions_e.swSaveWithReferencesOptions_None);

            if (advancedOptions != null)
            {
                logger.LogDebug("Got AdvancedSaveAsOptions for renamed document save");

                object? idsObj = null;
                object? namesObj = null;
                object? pathsObj = null;
                advancedOptions.GetItemsNameAndPath(out idsObj, out namesObj, out pathsObj);

                if (namesObj != null && pathsObj != null)
                {
                    logger.LogDebug("Got items from AdvancedSaveAsOptions, committing rename changes");
                    advancedOptions.ModifyItemsNameAndPath(idsObj, namesObj, pathsObj);
                }
            }

            var result = extension.SaveAs3(
                currentPath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_UpdateInactiveViews,
                advancedOptions,
                null,
                ref errors,
                ref warnings);

            logger.LogDebug("SaveAs3 result: {Result}, errors: {Errors}, warnings: {Warnings}", result, errors, warnings);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveWithRenamedReferences failed");
            errors = (int)swFileSaveError_e.swGenericSaveError;
            return false;
        }
        finally
        {
            if (assemblyDoc != null)
            {
                try { assemblyDoc.RenamedDocumentNotify -= assemblyHandler; } catch { }
            }

            if (partDoc != null)
            {
                try { partDoc.RenamedDocumentNotify -= partHandler; } catch { }
            }
        }
    }

    public static string DecodeSaveErrorBitmask(int errorCode)
    {
        if (errorCode == 0)
        {
            return "No errors";
        }

        var errors = new List<string>();
        var errorFlags = new Dictionary<int, string>
        {
            { 1, "swGenericSaveError (0x1) - Generic error" },
            { 2, "swReadOnlySaveError (0x2) - File is read-only" },
            { 4, "swFileNameEmpty (0x4) - Filename is empty" },
            { 8, "swFileNameContainsAtSign (0x8) - Filename contains @ symbol" },
            { 16, "swFileLockError (0x10) - File is locked by another process" },
            { 32, "swFileSaveFormatNotAvailable (0x20) - Save format not available" },
            { 64, "swFileSaveAsDoNotOverwrite (0x40) - File exists and overwrite disabled" },
            { 128, "swFileSaveAsInvalidFileExtension (0x80) - Invalid file extension" },
            { 256, "swFileSaveAsNoSelection (0x100) - No selection for SaveAs" },
            { 512, "swFileSaveAsBadEDrawingsVersion (0x200) - Bad eDrawings version" },
            { 1024, "swFileSaveAsNameExceedsMaxPathLength (0x400) - Path too long" },
            { 2048, "swFileSaveAsNotSupported (0x800) - SaveAs not supported" },
            { 4096, "swFileSaveRequiresSavingReferences (0x1000) - Must save references first" },
            { 8192, "swFileSaveRenamedDocumentHasUnsavedReferences (0x2000) - Renamed doc has unsaved references (COMMON!)" },
            { 65536, "swFileSaveWithRebuildError (0x10000) - Rebuild errors exist" },
            { 131072, "swFileSavePartialError (0x20000) - Partial save error" },
        };

        var remaining = errorCode;
        foreach (var flag in errorFlags.OrderByDescending(f => f.Key))
        {
            if ((remaining & flag.Key) == flag.Key)
            {
                errors.Add(flag.Value);
                remaining &= ~flag.Key;
            }
        }

        if (remaining != 0)
        {
            errors.Add($"Unknown flags: 0x{remaining:X}");
        }

        return errors.Count == 0 ? $"Unknown error code: {errorCode}" : string.Join(" | ", errors);
    }

    public static string FormatSaveError(int errorCode)
    {
        if (errorCode == 0)
        {
            return "None";
        }

        var decoded = DecodeSaveErrorBitmask(errorCode);
        return $"{errorCode} (0x{errorCode:X}) = {decoded}";
    }

    private static IReadOnlyList<string> BuildSearchFolders(ModelDoc2 model, IEnumerable<string>? searchFolders)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modelPath = model.GetPathName();
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            var directory = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                folders.Add(directory);
            }
        }

        if (searchFolders != null)
        {
            foreach (var folder in searchFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    folders.Add(Path.GetFullPath(folder));
                }
            }
        }

        return folders.ToArray();
    }
}
