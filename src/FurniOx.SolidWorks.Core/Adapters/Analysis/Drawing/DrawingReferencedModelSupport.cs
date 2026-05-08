using System;
using System.Collections.Generic;
using System.Linq;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Drawing;

internal static class DrawingReferencedModelSupport
{
    internal static List<ReferencedModel> Extract(DrawingDoc drawing, ILogger logger)
    {
        var models = new Dictionary<string, ReferencedModel>();

        try
        {
            var view = (drawing.GetFirstView() as View)?.GetNextView() as View;
            while (view != null)
            {
                var referencedDocument = view.ReferencedDocument as ModelDoc2;
                if (referencedDocument != null)
                {
                    var modelPath = referencedDocument.GetPathName() ?? string.Empty;
                    var configuration = view.ReferencedConfiguration?.ToString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(modelPath))
                    {
                        if (!models.ContainsKey(modelPath))
                        {
                            var modelType = referencedDocument.GetType() switch
                            {
                                1 => "Part",
                                2 => "Assembly",
                                _ => "Unknown"
                            };

                            models[modelPath] = new ReferencedModel
                            {
                                ModelPath = modelPath,
                                ModelType = modelType,
                                Configurations = new List<string>(),
                                ViewCount = 1
                            };
                        }
                        else
                        {
                            models[modelPath] = models[modelPath] with
                            {
                                ViewCount = models[modelPath].ViewCount + 1
                            };
                        }

                        if (!string.IsNullOrEmpty(configuration) && !models[modelPath].Configurations.Contains(configuration))
                        {
                            models[modelPath].Configurations.Add(configuration);
                        }
                    }
                }

                view = view.GetNextView() as View;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract referenced models");
        }

        return models.Values.ToList();
    }
}
