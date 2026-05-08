using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Temporarily controls whether newly opened SolidWorks documents create GUI windows.
/// This reduces GUI/graphics footprint during batch operations, but does not replace
/// closing documents opened by the tool.
/// </summary>
public sealed class DocumentVisibilityScope : IDisposable
{
    private readonly ISldWorks _app;
    private readonly List<(int Type, bool Visible)> _originalStates = new();
    private bool _disposed;

    private DocumentVisibilityScope(ISldWorks app, IEnumerable<int> documentTypes)
    {
        _app = app;

        foreach (var documentType in documentTypes.Distinct())
        {
            try
            {
                var original = _app.GetDocumentVisible(documentType);
                _originalStates.Add((documentType, original));
                if (original)
                {
                    _app.DocumentVisible(false, documentType);
                }
            }
            catch
            {
                // Some document types may not be supported by all SW versions.
            }
        }
    }

    public static DocumentVisibilityScope? HideNewDocuments(ISldWorks app, bool hiddenInGui, params int[] documentTypes)
    {
        if (!hiddenInGui)
        {
            return null;
        }

        var types = documentTypes.Length > 0
            ? documentTypes
            : new[]
            {
                (int)swDocumentTypes_e.swDocPART,
                (int)swDocumentTypes_e.swDocASSEMBLY,
                (int)swDocumentTypes_e.swDocDRAWING
            };

        return new DocumentVisibilityScope(app, types);
    }

    public static bool TryHide(ModelDoc2? model)
    {
        if (model == null)
        {
            return false;
        }

        try
        {
            model.Visible = false;
            return !model.Visible;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var (type, visible) in _originalStates.AsEnumerable().Reverse())
        {
            try
            {
                _app.DocumentVisible(visible, type);
            }
            catch
            {
            }
        }
    }
}
