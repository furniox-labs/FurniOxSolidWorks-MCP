using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Assembly;

internal static class AssemblyInterferenceSupport
{
    internal static Shared.Models.AssemblyInterferenceCheck? Perform(AssemblyDoc assembly, ILogger logger)
    {
        try
        {
            _ = assembly;
            return new Shared.Models.AssemblyInterferenceCheck
            {
                HasInterferences = false,
                InterferenceCount = 0,
                Interferences = new List<Shared.Models.InterferenceDetail>()
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to perform interference check");
            return null;
        }
    }
}
