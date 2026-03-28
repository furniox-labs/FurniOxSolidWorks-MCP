using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Connection;

internal static class SolidWorksConnectionFactory
{
    public static IReadOnlyList<string> BuildProgIds(SolidWorksSettings settings, ILogger logger)
    {
        var progIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (seen.Add(candidate))
            {
                progIds.Add(candidate);
            }
        }

        Add("SldWorks.Application");

        var versionHint = settings.GetProgIdVersionHint();
        if (!string.IsNullOrEmpty(versionHint))
        {
            if (int.TryParse(versionHint, out var parsedVersion) && parsedVersion >= 1900)
            {
                logger.LogWarning(
                    "SolidWorks ProgID version hint '{VersionHint}' looks like an installation year. " +
                    "Skipping version-specific ProgIDs and relying on generic discovery.",
                    versionHint);
                return progIds;
            }

            Add($"SldWorks.Application.{versionHint}");

            var digitsOnly = new string(versionHint.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
            if (!string.IsNullOrEmpty(digitsOnly))
            {
                Add($"SldWorks.Application.{digitsOnly}");
                if (!digitsOnly.Contains('.'))
                {
                    Add($"SldWorks.Application.{digitsOnly}.0");
                }
            }
        }

        return progIds;
    }

    public static SldWorks? TryGetRunningInstance(IReadOnlyList<string> progIds)
    {
        foreach (var progId in progIds)
        {
            try
            {
                var obj = ComHelper.GetActiveObject(progId);
                if (obj is SldWorks application)
                {
                    return application;
                }
            }
            catch (COMException)
            {
            }
        }

        return null;
    }

    public static SldWorks? TryCreateInstance(IReadOnlyList<string> progIds, ILogger logger)
    {
        foreach (var progId in progIds)
        {
            var type = Type.GetTypeFromProgID(progId);
            if (type == null)
            {
                continue;
            }

            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance is SldWorks application)
                {
                    application.Visible = true;
                    return application;
                }
            }
            catch (COMException)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error creating SolidWorks instance");
            }
        }

        return null;
    }
}
