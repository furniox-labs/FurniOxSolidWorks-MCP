using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Handles all configuration-related operations (7 operations)
/// </summary>
public class ConfigurationOperations : OperationHandlerBase
{
    public ConfigurationOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<ConfigurationOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Configuration.GetConfigurationNames" => GetConfigurationNamesAsync(cancellationToken),
            "Configuration.ActivateConfiguration" => ActivateConfigurationAsync(parameters, cancellationToken),
            "Configuration.AddConfiguration" => AddConfigurationAsync(parameters, cancellationToken),
            "Configuration.DeleteConfiguration" => DeleteConfigurationAsync(parameters, cancellationToken),
            "Configuration.CopyConfiguration" => CopyConfigurationAsync(parameters, cancellationToken),
            "Configuration.GetConfigurationCount" => GetConfigurationCountAsync(cancellationToken),
            "Configuration.ShowConfiguration" => ShowConfigurationAsync(parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown configuration operation: {operation}"))
        };
    }

    private Task<ExecutionResult> GetConfigurationNamesAsync(CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // GetConfigurationNames returns string array
        var configNames = model.GetConfigurationNames().ToStringArraySafe();

        if (configNames == null || configNames.Length == 0)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Count = 0,
                ConfigurationNames = Array.Empty<string>()
            }));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Count = configNames.Length,
            ConfigurationNames = configNames
        }));
    }

    private Task<ExecutionResult> ActivateConfigurationAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract configuration name parameter
        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string configName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        // Get configuration
        var config = (IConfiguration?)model.GetConfigurationByName(configName);
        if (config == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Configuration '{configName}' not found"));
        }

        // ShowConfiguration2 activates and displays the configuration
        bool result = model.ShowConfiguration2(configName);

        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to activate configuration '{configName}'"));
        }

        // Verify active configuration (must cast to Configuration)
        var activeConfig = (Configuration?)model.GetActiveConfiguration();

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Activated = true,
            ConfigurationName = configName,
            IsActive = activeConfig != null && activeConfig.Name == configName
        }));
    }

    private Task<ExecutionResult> AddConfigurationAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract configuration name parameter
        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string configName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        // Get optional description
        var description = GetStringParam(parameters, "Description");

        // Get optional alternate name
        var alternateName = GetStringParam(parameters, "AlternateName");

        // Get optional base configuration (to derive from)
        var baseConfigName = GetStringParam(parameters, "BaseConfiguration");

        // AddConfiguration3 signature (4 parameters):
        // string ConfigName, string Comment, string AlternateName, int Options
        var config = (IConfiguration?)model.AddConfiguration3(
            configName,
            description,
            alternateName,
            (int)swConfigurationOptions2_e.swConfigOption_DontActivate
        );

        if (config == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to create configuration '{configName}'"));
        }

        // If baseConfiguration specified, copy its custom properties to the new config
        int propertiesCopied = 0;
        if (!string.IsNullOrEmpty(baseConfigName))
        {
            try
            {
                var sourcePropMgr = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[baseConfigName];
                var targetPropMgr = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[configName];

                if (sourcePropMgr != null && targetPropMgr != null)
                {
                    object? propNamesObj = null, propTypesObj = null, propValuesObj = null;
                    sourcePropMgr.GetAll(ref propNamesObj, ref propTypesObj, ref propValuesObj);

                    var propNames = propNamesObj.ToObjectArraySafe();
                    var propTypes = propTypesObj.ToObjectArraySafe();
                    var propValues = propValuesObj.ToObjectArraySafe();

                    if (propNames != null && propTypes != null && propValues != null)
                    {
                        for (int i = 0; i < propNames.Length; i++)
                        {
                            var propName = propNames[i]?.ToString();
                            var propType = Convert.ToInt32(propTypes[i]);
                            var propValue = propValues[i]?.ToString();

                            if (!string.IsNullOrEmpty(propName) && !string.IsNullOrEmpty(propValue))
                            {
                                targetPropMgr.Add3(propName, propType, propValue,
                                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                                propertiesCopied++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy properties from base config '{BaseConfig}' to '{NewConfig}'",
                    baseConfigName, configName);
            }
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Created = true,
            ConfigurationName = config.Name,
            Description = description,
            AlternateName = alternateName,
            BaseConfiguration = string.IsNullOrEmpty(baseConfigName) ? null : baseConfigName,
            PropertiesCopied = propertiesCopied
        }));
    }

    private Task<ExecutionResult> DeleteConfigurationAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract configuration name parameter
        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string configName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        // Get configuration
        var config = (IConfiguration?)model.GetConfigurationByName(configName);
        if (config == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Configuration '{configName}' not found"));
        }

        // Cannot delete active configuration - switch to another first (must cast to Configuration)
        var activeConfig = (Configuration?)model.GetActiveConfiguration();
        if (activeConfig != null && activeConfig.Name == configName)
        {
            return Task.FromResult(ExecutionResult.Failure($"Cannot delete active configuration '{configName}'. Activate a different configuration first."));
        }

        // DeleteConfiguration2 deletes the configuration
        bool result = model.DeleteConfiguration2(configName);

        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to delete configuration '{configName}'"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Deleted = true,
            ConfigurationName = configName
        }));
    }

    private Task<ExecutionResult> CopyConfigurationAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract source configuration name
        if (!parameters.TryGetValue("SourceName", out var sourceNameObj) || sourceNameObj is not string sourceConfigName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'SourceName' parameter"));
        }

        // Extract target configuration name
        if (!parameters.TryGetValue("TargetName", out var targetNameObj) || targetNameObj is not string targetConfigName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'TargetName' parameter"));
        }

        // Get source configuration
        var sourceConfig = (IConfiguration?)model.GetConfigurationByName(sourceConfigName);
        if (sourceConfig == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Source configuration '{sourceConfigName}' not found"));
        }

        // Get optional description (use source config description if not provided)
        var description = GetStringParam(parameters, "Description");
        if (string.IsNullOrEmpty(description))
        {
            description = sourceConfig.Comment;
        }

        // IConfiguration.Copy() doesn't exist - use multi-step approach
        // Step 1: Create new configuration using AddConfiguration3
        var targetConfig = (IConfiguration?)model.AddConfiguration3(
            targetConfigName,
            description,
            sourceConfig.AlternateName,
            (int)swConfigurationOptions2_e.swConfigOption_DontActivate
        );

        if (targetConfig == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to create configuration '{targetConfigName}'"));
        }

        // Step 2: Copy custom properties (parameter copying requires different API approach)
        try
        {
            var sourcePropMgr = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[sourceConfigName];
            var targetPropMgr = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[targetConfigName];

            if (sourcePropMgr != null && targetPropMgr != null)
            {
                object? propNamesObj = null;
                object? propTypesObj = null;
                object? propValuesObj = null;

                sourcePropMgr.GetAll(ref propNamesObj, ref propTypesObj, ref propValuesObj);

                var propNames = propNamesObj.ToObjectArraySafe();
                var propTypes = propTypesObj.ToObjectArraySafe();
                var propValues = propValuesObj.ToObjectArraySafe();

                if (propNames != null && propTypes != null && propValues != null)
                {
                    for (int i = 0; i < propNames.Length; i++)
                    {
                        string? propName = propNames[i]?.ToString();
                        int propType = Convert.ToInt32(propTypes[i]);
                        string? propValue = propValues[i]?.ToString();

                        if (!string.IsNullOrEmpty(propName) && !string.IsNullOrEmpty(propValue))
                        {
                            targetPropMgr.Add3(propName, propType, propValue,
                                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy custom properties from '{SourceConfig}' to '{TargetConfig}'",
                sourceConfigName, targetConfigName);
        }

        // Get count of custom properties copied
        int propertiesCopied = 0;
        try
        {
            var targetPropMgr = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[targetConfigName];
            if (targetPropMgr != null)
            {
                propertiesCopied = targetPropMgr.Count;
            }
        }
        catch { }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Copied = true,
            SourceConfiguration = sourceConfigName,
            TargetConfiguration = targetConfig.Name,
            Description = targetConfig.Comment,
            CustomPropertiesCopied = propertiesCopied
        }));
    }

    private Task<ExecutionResult> GetConfigurationCountAsync(CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // GetConfigurationCount returns int
        int count = model.GetConfigurationCount();

        // Get active configuration name (must cast to Configuration)
        var activeConfig = (Configuration?)model.GetActiveConfiguration();
        var activeConfigName = activeConfig?.Name;

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Count = count,
            ActiveConfiguration = activeConfigName
        }));
    }

    private Task<ExecutionResult> ShowConfigurationAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        // Extract configuration name parameter
        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string configName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        // ShowConfiguration2 displays the configuration (activates it)
        bool result = model.ShowConfiguration2(configName);

        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to show configuration '{configName}'"));
        }

        // Verify it's now active (must cast to Configuration)
        var activeConfig = (Configuration?)model.GetActiveConfiguration();
        bool isActive = activeConfig != null && activeConfig.Name == configName;

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Shown = true,
            ConfigurationName = configName,
            IsActive = isActive
        }));
    }
}
