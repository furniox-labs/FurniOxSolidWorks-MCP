using System;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

internal static class DocumentRenameSupport
{
    public static string GetRenameErrorMessage(swRenameDocumentError_e error)
    {
        return error switch
        {
            swRenameDocumentError_e.swRenameDocumentError_None => "Success",
            swRenameDocumentError_e.swRenameDocumentError_UnspecifiedInternalError => "Unspecified internal error",
            swRenameDocumentError_e.swRenameDocumentError_InvalidSelection => "No component selected or invalid selection",
            swRenameDocumentError_e.swRenameDocumentError_InvalidForDrawings => "Cannot rename drawings with this method - use SaveAs",
            swRenameDocumentError_e.swRenameDocumentError_NoModelLoaded => "Document is not loaded",
            swRenameDocumentError_e.swRenameDocumentError_ComponentNotResolved => "Component is suppressed or unresolved - resolve first",
            swRenameDocumentError_e.swRenameDocumentError_LightWeightComponent => "Component is lightweight - set to resolved state first",
            swRenameDocumentError_e.swRenameDocumentError_RoutingComponent => "Routing component cannot be renamed",
            swRenameDocumentError_e.swRenameDocumentError_FileAlreadyExists => "Target filename already exists",
            swRenameDocumentError_e.swRenameDocumentError_InvalidCharactersInName => "Filename contains invalid characters",
            swRenameDocumentError_e.swRenameDocumentError_InvalidVirtualComponent => "Virtual component issue",
            swRenameDocumentError_e.swRenameDocumentError_NameTooLong => "Filename exceeds maximum path length",
            swRenameDocumentError_e.swRenameDocumentError_DocumentNameInUse => "Document name conflicts with another open document",
            swRenameDocumentError_e.swRenameDocumentError_PendingNameAlreadyInUse => "Pending rename conflicts - process pending renames first",
            swRenameDocumentError_e.swRenameDocumentError_ReadOnlyDocument => "Document is read-only - remove read-only attribute",
            swRenameDocumentError_e.swRenameDocumentError_DocumentNotSaved => "Document has never been saved - save first",
            swRenameDocumentError_e.swRenameDocumentError_VirtualComponent => "Virtual component restriction - save externally first",
            swRenameDocumentError_e.swRenameDocumentError_NotAllowedWithPDM => "PDM environment detected - use PDM API instead",
            swRenameDocumentError_e.swRenameDocumentError_ToolboxComponent => "Toolbox parts cannot be renamed",
            swRenameDocumentError_e.swRenameDocumentError_PatternedComponent => "Patterned component instance - rename pattern seed only",
            _ => $"Unknown error code: {(int)error}"
        };
    }

    public static bool TryNormalizeNewName(
        string newName,
        string expectedExtension,
        string targetDescription,
        out string baseNameForApi,
        out string? errorMessage)
    {
        errorMessage = null;
        baseNameForApi = newName;

        if (string.IsNullOrWhiteSpace(expectedExtension))
        {
            return true;
        }

        if (newName.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            baseNameForApi = newName.Substring(0, newName.Length - expectedExtension.Length);
            return true;
        }

        foreach (var wrongExtension in new[] { ".SLDPRT", ".SLDASM", ".SLDDRW" })
        {
            if (newName.EndsWith(wrongExtension, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Wrong file extension. Expected {expectedExtension} for {targetDescription}, but got {wrongExtension}.";
                return false;
            }
        }

        return true;
    }

    public static string GetExpectedExtension(int docType)
    {
        return docType switch
        {
            (int)swDocumentTypes_e.swDocPART => ".SLDPRT",
            (int)swDocumentTypes_e.swDocASSEMBLY => ".SLDASM",
            _ => string.Empty
        };
    }

    public static int GetDocumentTypeFromPath(string path)
    {
        if (path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase))
        {
            return (int)swDocumentTypes_e.swDocPART;
        }

        if (path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
        {
            return (int)swDocumentTypes_e.swDocASSEMBLY;
        }

        return (int)swDocumentTypes_e.swDocPART;
    }
}
