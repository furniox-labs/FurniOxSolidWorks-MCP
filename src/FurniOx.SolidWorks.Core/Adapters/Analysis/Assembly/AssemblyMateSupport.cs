using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Assembly;

internal static class AssemblyMateSupport
{
    internal static List<Shared.Models.AssemblyMate> Extract(ModelDoc2 model)
    {
        var mates = new List<Shared.Models.AssemblyMate>();
        var feature = model.FirstFeature() as Feature;

        while (feature != null)
        {
            if (feature.GetTypeName() == "MateGroup")
            {
                var subFeature = feature.GetFirstSubFeature() as Feature;
                while (subFeature != null)
                {
                    var mate = subFeature.GetSpecificFeature2() as IMate2;
                    if (mate != null)
                    {
                        var entities = new List<Shared.Models.MateEntity>();
                        for (var index = 0; index < mate.GetMateEntityCount(); index++)
                        {
                            if (mate.MateEntity(index) is not IMateEntity2 mateEntity)
                            {
                                continue;
                            }

                            var component = mateEntity.ReferenceComponent as IComponent2;
                            var reference = mateEntity.Reference;
                            entities.Add(new Shared.Models.MateEntity
                            {
                                ComponentName = component?.Name2 ?? "Unknown",
                                EntityType = AnalysisHelpers.GetEntityTypeName(reference),
                                EntityName = AnalysisHelpers.GetEntityName(reference)
                            });
                        }

                        var isWarning = false;
                        var errorCode = subFeature.GetErrorCode2(out isWarning);

                        mates.Add(new Shared.Models.AssemblyMate
                        {
                            Name = subFeature.Name,
                            Type = AnalysisHelpers.GetMateTypeName(mate.Type),
                            TypeCode = mate.Type,
                            Suppressed = subFeature.IsSuppressed(),
                            Broken = errorCode != 0,
                            Entities = entities
                        });
                    }

                    subFeature = subFeature.GetNextSubFeature() as Feature;
                }
            }

            feature = feature.GetNextFeature() as Feature;
        }

        return mates;
    }
}
