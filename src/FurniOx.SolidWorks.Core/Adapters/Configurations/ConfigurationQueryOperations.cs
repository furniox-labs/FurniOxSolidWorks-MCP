using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Configurations;

public sealed class ConfigurationQueryOperations : OperationHandlerBase
{
    public ConfigurationQueryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<ConfigurationQueryOperations> logger)
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
            "Configuration.GetConfigurationNames" => GetConfigurationNamesAsync(),
            "Configuration.ActivateConfiguration" => ActivateConfigurationAsync(parameters),
            "Configuration.GetConfigurationCount" => GetConfigurationCountAsync(),
            "Configuration.ShowConfiguration" => ShowConfigurationAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown configuration query operation: {operation}"))
        };
    }

    private Task<ExecutionResult> GetConfigurationNamesAsync()
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        var configurationNames = model.GetConfigurationNames().ToStringArraySafe();
        if (configurationNames.Length == 0)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Count = 0,
                ConfigurationNames = Array.Empty<string>()
            }));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Count = configurationNames.Length,
            ConfigurationNames = configurationNames
        }));
    }

    private Task<ExecutionResult> ActivateConfigurationAsync(IDictionary<string, object?> parameters)
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string configurationName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        var configuration = (IConfiguration?)model.GetConfigurationByName(configurationName);
        if (configuration == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Configuration '{configurationName}' not found"));
        }

        var result = model.ShowConfiguration2(configurationName);
        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to activate configuration '{configurationName}'"));
        }

        var activeConfiguration = (Configuration?)model.GetActiveConfiguration();
        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Activated = true,
            ConfigurationName = configurationName,
            IsActive = activeConfiguration != null && activeConfiguration.Name == configurationName
        }));
    }

    private Task<ExecutionResult> GetConfigurationCountAsync()
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        var activeConfiguration = (Configuration?)model.GetActiveConfiguration();
        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Count = model.GetConfigurationCount(),
            ActiveConfiguration = activeConfiguration?.Name
        }));
    }

    private Task<ExecutionResult> ShowConfigurationAsync(IDictionary<string, object?> parameters)
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string configurationName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        var result = model.ShowConfiguration2(configurationName);
        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to show configuration '{configurationName}'"));
        }

        var activeConfiguration = (Configuration?)model.GetActiveConfiguration();
        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Shown = true,
            ConfigurationName = configurationName,
            IsActive = activeConfiguration != null && activeConfiguration.Name == configurationName
        }));
    }

    private ModelDoc2? GetActiveModel()
    {
        return (ModelDoc2?)_connection.Application?.ActiveDoc;
    }

    private ExecutionResult NotConnectedOrNoDocument()
    {
        return _connection.Application == null
            ? ExecutionResult.Failure("Not connected to SolidWorks")
            : ExecutionResult.Failure("No active document");
    }
}
