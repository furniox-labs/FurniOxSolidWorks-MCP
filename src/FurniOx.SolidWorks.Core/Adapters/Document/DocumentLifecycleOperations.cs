using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

public sealed class DocumentLifecycleOperations : OperationHandlerBase
{
    private readonly DocumentFileOperations _fileOperations;
    private readonly DocumentSessionOperations _sessionOperations;

    public DocumentLifecycleOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentLifecycleOperations> logger,
        DocumentFileOperations fileOperations,
        DocumentSessionOperations sessionOperations)
        : base(connection, settings, logger)
    {
        _fileOperations = fileOperations;
        _sessionOperations = sessionOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (DocumentOperationNames.File.Contains(operation))
        {
            return _fileOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        if (DocumentOperationNames.Session.Contains(operation))
        {
            return _sessionOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        return Task.FromResult(ExecutionResult.Failure($"Unknown document lifecycle operation: {operation}"));
    }
}
