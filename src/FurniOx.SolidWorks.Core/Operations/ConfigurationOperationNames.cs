using System.Collections.Generic;

namespace FurniOx.SolidWorks.Core.Operations;

public static class ConfigurationOperationNames
{
    public const string GetConfigurationNames = "Configuration.GetConfigurationNames";
    public const string ActivateConfiguration = "Configuration.ActivateConfiguration";
    public const string AddConfiguration = "Configuration.AddConfiguration";
    public const string DeleteConfiguration = "Configuration.DeleteConfiguration";
    public const string CopyConfiguration = "Configuration.CopyConfiguration";
    public const string GetConfigurationCount = "Configuration.GetConfigurationCount";
    public const string ShowConfiguration = "Configuration.ShowConfiguration";

    public static readonly IReadOnlyList<string> All =
    [
        GetConfigurationNames,
        ActivateConfiguration,
        AddConfiguration,
        DeleteConfiguration,
        CopyConfiguration,
        GetConfigurationCount,
        ShowConfiguration
    ];
}
