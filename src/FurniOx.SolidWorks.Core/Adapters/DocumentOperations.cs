using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Document;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class DocumentOperations : OperationHandlerBase
{
    private readonly DocumentLifecycleOperations _lifecycleOperations;
    private readonly DocumentQueryOperations _queryOperations;

    public DocumentOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentOperations> logger,
        DocumentLifecycleOperations lifecycleOperations,
        DocumentQueryOperations queryOperations)
        : base(connection, settings, logger)
    {
        _lifecycleOperations = lifecycleOperations;
        _queryOperations = queryOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (DocumentOperationNames.Lifecycle.Contains(operation))
        {
            return _lifecycleOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        if (DocumentOperationNames.Query.Contains(operation))
        {
            return _queryOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        return Task.FromResult(ExecutionResult.Failure($"Unknown document operation: {operation}"));
    }
}
