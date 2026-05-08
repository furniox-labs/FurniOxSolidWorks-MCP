using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

/// <summary>
/// Assembly-context resolution helpers for part analysis.
/// Handles detecting in-context editing and resolving FeatureManager folder paths.
/// </summary>
internal static class PartAnalysisContextSupport
{
    /// <summary>
    /// Extracts assembly context when a part is being edited in-context within an assembly.
    /// Returns null if the part is opened standalone (not in assembly context).
    /// </summary>
    internal static PartAssemblyContext? ExtractPartAssemblyContext(
        SldWorks app,
        ModelDoc2 partModel,
        ILogger logger)
    {
        try
        {
            var docsArray = app.GetDocuments().ToObjectArraySafe();
            if (docsArray == null)
            {
                return null;
            }

            foreach (var docObj in docsArray)
            {
                var doc = docObj as ModelDoc2;
                if (doc == null)
                {
                    continue;
                }

                if (doc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    continue;
                }

                var assemblyDoc = doc as AssemblyDoc;
                if (assemblyDoc == null)
                {
                    continue;
                }

                var editTarget = assemblyDoc.GetEditTarget() as Component2;
                if (editTarget == null)
                {
                    continue;
                }

                var editTargetModel = editTarget.GetModelDoc2() as ModelDoc2;
                if (editTargetModel == null)
                {
                    continue;
                }

                var partPath = partModel.GetPathName();
                var editTargetPath = editTargetModel.GetPathName();

                if (!string.IsNullOrEmpty(partPath) && partPath.Equals(editTargetPath, StringComparison.OrdinalIgnoreCase))
                {
                    var xform = (MathTransform?)editTarget.Transform2;
                    if (xform == null)
                    {
                        return null;
                    }

                    var arrayData = (double[]?)xform.ArrayData;
                    if (arrayData == null || arrayData.Length < 16)
                    {
                        return null;
                    }

                    return new PartAssemblyContext
                    {
                        ParentAssemblyName = doc.GetTitle(),
                        ParentAssemblyPath = doc.GetPathName(),
                        ComponentName = editTarget.Name2 ?? "",
                        OriginInAssembly = new Point3D
                        {
                            X = AnalysisHelpers.MetersToMm(arrayData[9]),
                            Y = AnalysisHelpers.MetersToMm(arrayData[10]),
                            Z = AnalysisHelpers.MetersToMm(arrayData[11])
                        },
                        TransformMatrix = arrayData,
                        RotationMatrix = new[]
                        {
                            arrayData[0], arrayData[1], arrayData[2],
                            arrayData[3], arrayData[4], arrayData[5],
                            arrayData[6], arrayData[7], arrayData[8]
                        }
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract part assembly context");
            return null;
        }
    }

    /// <summary>
    /// Resolves the FeatureManager folder path for a component identified by its instance path
    /// (e.g. "SubAssembly-1/Part-1"). Walks nested sub-assemblies by opening them if needed.
    /// </summary>
    internal static string? TryGetFolderPathForComponentInstancePath(
        ISldWorks app,
        ModelDoc2 rootAssemblyModel,
        AssemblyDoc rootAssembly,
        string instancePath,
        bool openReferencedDocs,
        out ModelDoc2? openedDoc,
        out bool openedByUs,
        Dictionary<string, IComponent2>? prebuiltIndex = null)
    {
        openedDoc = null;
        openedByUs = false;

        if (string.IsNullOrWhiteSpace(instancePath))
        {
            return null;
        }

        if (!instancePath.Contains('/'))
        {
            var folderMap = AnalysisHelpers.GetTopLevelComponentFolderMembership(rootAssemblyModel, rootAssembly);
            return folderMap.TryGetValue(instancePath, out var folderPath) ? folderPath : null;
        }

        var lastSlash = instancePath.LastIndexOf('/');
        if (lastSlash <= 0 || lastSlash >= instancePath.Length - 1)
        {
            return null;
        }

        var parentPath = instancePath.Substring(0, lastSlash);
        var childLocalName = instancePath.Substring(lastSlash + 1);

        var index = prebuiltIndex ?? AnalysisHelpers.BuildComponentIndex(rootAssembly);
        if (!index.TryGetValue(parentPath, out var parentComponent))
        {
            return null;
        }

        var parentFilePath = parentComponent.GetPathName();
        if (string.IsNullOrWhiteSpace(parentFilePath))
        {
            return null;
        }

        var subModel = parentComponent.GetModelDoc2() as ModelDoc2;
        if (subModel == null && openReferencedDocs)
        {
            subModel = AnalysisHelpers.TryOpenModelIfNeeded(app, parentFilePath, swDocumentTypes_e.swDocASSEMBLY, out openedByUs);
            openedDoc = subModel;
        }

        if (subModel == null || subModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return null;
        }

        var subAssembly = (AssemblyDoc)subModel;
        var folderMapInSubAssembly = AnalysisHelpers.GetTopLevelComponentFolderMembership(subModel, subAssembly);
        return folderMapInSubAssembly.TryGetValue(childLocalName, out var folderPathInSubAssembly) ? folderPathInSubAssembly : null;
    }
}
