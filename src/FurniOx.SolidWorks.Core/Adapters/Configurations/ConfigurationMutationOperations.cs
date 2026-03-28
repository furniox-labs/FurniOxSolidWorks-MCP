using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Configurations;

public sealed class ConfigurationMutationOperations : OperationHandlerBase
{
    public ConfigurationMutationOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<ConfigurationMutationOperations> logger)
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
            "Configuration.AddConfiguration" => AddConfigurationAsync(parameters),
            "Configuration.DeleteConfiguration" => DeleteConfigurationAsync(parameters),
            "Configuration.CopyConfiguration" => CopyConfigurationAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown configuration mutation operation: {operation}"))
        };
    }

    private Task<ExecutionResult> AddConfigurationAsync(IDictionary<string, object?> parameters)
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

        var description = GetStringParam(parameters, "Description");
        var alternateName = GetStringParam(parameters, "AlternateName");
        var baseConfigurationName = GetStringParam(parameters, "BaseConfiguration");

        var configuration = (IConfiguration?)model.AddConfiguration3(
            configurationName,
            description,
            alternateName,
            (int)swConfigurationOptions2_e.swConfigOption_DontActivate);
        if (configuration == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to create configuration '{configurationName}'"));
        }

        var propertiesCopied = string.IsNullOrEmpty(baseConfigurationName)
            ? 0
            : ConfigurationPropertyCopySupport.CopyCustomProperties(model, baseConfigurationName, configurationName, _logger);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Created = true,
            ConfigurationName = configuration.Name,
            Description = description,
            AlternateName = alternateName,
            BaseConfiguration = string.IsNullOrEmpty(baseConfigurationName) ? null : baseConfigurationName,
            PropertiesCopied = propertiesCopied
        }));
    }

    private Task<ExecutionResult> DeleteConfigurationAsync(IDictionary<string, object?> parameters)
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

        var activeConfiguration = (Configuration?)model.GetActiveConfiguration();
        if (activeConfiguration != null && activeConfiguration.Name == configurationName)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Cannot delete active configuration '{configurationName}'. Activate a different configuration first."));
        }

        var result = model.DeleteConfiguration2(configurationName);
        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to delete configuration '{configurationName}'"));
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Deleted = true,
            ConfigurationName = configurationName
        }));
    }

    private Task<ExecutionResult> CopyConfigurationAsync(IDictionary<string, object?> parameters)
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        if (!parameters.TryGetValue("SourceName", out var sourceNameObj) || sourceNameObj is not string sourceConfigurationName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'SourceName' parameter"));
        }

        if (!parameters.TryGetValue("TargetName", out var targetNameObj) || targetNameObj is not string targetConfigurationName)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'TargetName' parameter"));
        }

        var sourceConfiguration = (IConfiguration?)model.GetConfigurationByName(sourceConfigurationName);
        if (sourceConfiguration == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Source configuration '{sourceConfigurationName}' not found"));
        }

        var description = GetStringParam(parameters, "Description");
        if (string.IsNullOrEmpty(description))
        {
            description = sourceConfiguration.Comment;
        }

        var targetConfiguration = (IConfiguration?)model.AddConfiguration3(
            targetConfigurationName,
            description,
            sourceConfiguration.AlternateName,
            (int)swConfigurationOptions2_e.swConfigOption_DontActivate);
        if (targetConfiguration == null)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to create configuration '{targetConfigurationName}'"));
        }

        var propertiesCopied = ConfigurationPropertyCopySupport.CopyCustomProperties(
            model,
            sourceConfigurationName,
            targetConfigurationName,
            _logger);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Copied = true,
            SourceConfiguration = sourceConfigurationName,
            TargetConfiguration = targetConfiguration.Name,
            Description = targetConfiguration.Comment,
            CustomPropertiesCopied = propertiesCopied
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
