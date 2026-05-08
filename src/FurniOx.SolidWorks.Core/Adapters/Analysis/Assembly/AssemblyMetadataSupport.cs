using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Assembly;

internal static class AssemblyMetadataSupport
{
    internal static Shared.Models.AssemblyMetadata Extract(
        ModelDoc2 model,
        AssemblyDoc assembly,
        Dictionary<string, IComponent2>? prebuiltIndex = null)
    {
        var activeConfiguration = model.ConfigurationManager.ActiveConfiguration as Configuration;
        var componentIndex = prebuiltIndex ?? AnalysisHelpers.BuildComponentIndex(assembly);
        var componentCount = componentIndex.Count;

        var partCount = 0;
        var subAssemblyCount = 0;
        foreach (var component in componentIndex.Values)
        {
            var path = component.GetPathName();
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            var extension = Path.GetExtension(path).ToUpperInvariant();
            if (extension == ".SLDPRT")
            {
                partCount++;
            }
            else if (extension == ".SLDASM")
            {
                subAssemblyCount++;
            }
        }

        return new Shared.Models.AssemblyMetadata
        {
            Name = model.GetTitle(),
            Path = model.GetPathName() ?? string.Empty,
            Type = "Assembly",
            ConfigurationName = activeConfiguration?.Name ?? "Default",
            ComponentCount = componentCount,
            TotalParts = partCount,
            TotalSubAssemblies = subAssemblyCount,
            TotalMates = 0,
            IsTopLevel = true
        };
    }
}
