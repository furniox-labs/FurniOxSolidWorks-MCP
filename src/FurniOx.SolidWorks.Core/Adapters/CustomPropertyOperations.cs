using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Custom property coordinator - delegates to focused read/write handlers.
/// </summary>
public sealed class CustomPropertyOperations : OperationHandlerBase
{
    private readonly CustomPropertyReadOperations _readOperations;
    private readonly CustomPropertyWriteOperations _writeOperations;

    internal CustomPropertyOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<CustomPropertyOperations> logger,
        CustomPropertyReadOperations readOperations,
        CustomPropertyWriteOperations writeOperations)
        : base(connection, settings, logger)
    {
        _readOperations = readOperations;
        _writeOperations = writeOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "CustomProperty.Get" or "CustomProperty.GetAll"
                => _readOperations.ExecuteAsync(operation, parameters, cancellationToken),
            "CustomProperty.Set" or "CustomProperty.Delete"
                => _writeOperations.ExecuteAsync(operation, parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown custom property operation: {operation}"))
        };
    }
}
