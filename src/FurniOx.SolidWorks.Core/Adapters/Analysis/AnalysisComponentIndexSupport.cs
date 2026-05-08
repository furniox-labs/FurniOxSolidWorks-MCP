using System;
using System.Collections.Generic;
using System.IO;
using FurniOx.SolidWorks.Core.Extensions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

internal static class AnalysisComponentIndexSupport
{
    public static Dictionary<string, IComponent2> BuildComponentIndex(AssemblyDoc assembly)
    {
        var index = new Dictionary<string, IComponent2>(StringComparer.OrdinalIgnoreCase);
        var attemptedResolve = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var topLevelComponents = assembly.GetComponents(true).ToObjectArraySafe();
        if (topLevelComponents != null)
        {
            foreach (var componentObject in topLevelComponents)
            {
                if (componentObject is IComponent2 component)
                {
                    BuildComponentIndexRecursive(component, index, attemptedResolve);
                }
            }
        }

        return index;
    }

    private static void BuildComponentIndexRecursive(
        IComponent2 component,
        Dictionary<string, IComponent2> index,
        HashSet<string> attemptedResolve)
    {
        var name2 = component.Name2;
        if (!string.IsNullOrEmpty(name2) && !index.ContainsKey(name2))
        {
            index[name2] = component;
        }

        var children = TryGetChildrenWithBestEffortResolve(component, attemptedResolve);
        if (children == null || children.Length == 0)
        {
            return;
        }

        foreach (var childObject in children)
        {
            if (childObject is IComponent2 child)
            {
                BuildComponentIndexRecursive(child, index, attemptedResolve);
            }
        }
    }

    private static object[]? TryGetChildrenWithBestEffortResolve(IComponent2 component, HashSet<string> attemptedResolve)
    {
        object[]? children = null;

        try { children = component.GetChildren().ToObjectArraySafe(); } catch { children = null; }
        if (children != null && children.Length > 0)
        {
            return children;
        }

        if (!IsAssemblyComponent(component))
        {
            return children;
        }

        var key = component.Name2 ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key) || attemptedResolve.Contains(key))
        {
            return children;
        }

        attemptedResolve.Add(key);
        if (!TryResolveComponent(component))
        {
            return children;
        }

        try { return component.GetChildren().ToObjectArraySafe(); } catch { return children; }
    }

    private static bool IsAssemblyComponent(IComponent2 component)
    {
        try
        {
            var path = component.GetPathName();
            if (!string.IsNullOrWhiteSpace(path))
            {
                var extension = Path.GetExtension(path);
                if (extension.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (extension.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }
        catch
        {
        }

        try
        {
            var document = component.GetModelDoc2() as ModelDoc2;
            return document != null && document.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveComponent(IComponent2 component)
    {
        try
        {
            if (component.GetModelDoc2() is ModelDoc2)
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            var suppressionState = component.GetSuppression2();
            if (suppressionState == (int)swComponentSuppressionState_e.swComponentSuppressed)
            {
                return false;
            }
        }
        catch
        {
        }

        try
        {
            component.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
        }
        catch
        {
            return false;
        }

        try
        {
            return component.GetModelDoc2() is ModelDoc2;
        }
        catch
        {
            return false;
        }
    }
}
