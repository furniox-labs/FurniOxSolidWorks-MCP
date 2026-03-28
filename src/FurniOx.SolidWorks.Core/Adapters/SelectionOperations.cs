using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Adapters.Selections;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class SelectionOperations : OperationHandlerBase
{
    private readonly SelectionEntityOperations _entityOperations;
    private readonly SelectionComponentOperations _componentOperations;

    public SelectionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
        : this(
            connection,
            settings,
            loggerFactory.CreateLogger<SelectionOperations>(),
            new SelectionEntityOperations(connection, settings, loggerFactory.CreateLogger<SelectionEntityOperations>()),
            new SelectionComponentOperations(connection, settings, loggerFactory.CreateLogger<SelectionComponentOperations>()))
    {
    }

    internal SelectionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SelectionOperations> logger,
        SelectionEntityOperations entityOperations,
        SelectionComponentOperations componentOperations)
        : base(connection, settings, logger)
    {
        _entityOperations = entityOperations;
        _componentOperations = componentOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Selection.SelectByID2" => _entityOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Selection.SelectComponent" => _componentOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Selection.ClearSelection2" => _entityOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "Selection.DeleteSelection2" => _entityOperations.ExecuteAsync(operation, parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown selection operation: {operation}"))
        };
    }
}
