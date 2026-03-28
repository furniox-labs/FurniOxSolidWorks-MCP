using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Configurations;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class ConfigurationOperations : OperationHandlerBase
{
    private readonly ConfigurationQueryOperations _queryOperations;
    private readonly ConfigurationMutationOperations _mutationOperations;

    public ConfigurationOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
        : this(
            connection,
            settings,
            loggerFactory.CreateLogger<ConfigurationOperations>(),
            new ConfigurationQueryOperations(connection, settings, loggerFactory.CreateLogger<ConfigurationQueryOperations>()),
            new ConfigurationMutationOperations(connection, settings, loggerFactory.CreateLogger<ConfigurationMutationOperations>()))
    {
    }

    internal ConfigurationOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<ConfigurationOperations> logger,
        ConfigurationQueryOperations queryOperations,
        ConfigurationMutationOperations mutationOperations)
        : base(connection, settings, logger)
    {
        _queryOperations = queryOperations;
        _mutationOperations = mutationOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Configuration.GetConfigurationNames" => _queryOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Configuration.ActivateConfiguration" => _queryOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Configuration.AddConfiguration" => _mutationOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Configuration.DeleteConfiguration" => _mutationOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Configuration.CopyConfiguration" => _mutationOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Configuration.GetConfigurationCount" => _queryOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Configuration.ShowConfiguration" => _queryOperations.ExecuteAsync(operation, parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown configuration operation: {operation}"))
        };
    }
}
