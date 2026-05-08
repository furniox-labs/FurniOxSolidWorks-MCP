using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

public sealed class DocumentRenameOperations : OperationHandlerBase
{
    private readonly DocumentFileRenameOperations _fileRenameOperations;
    private readonly DocumentComponentFileRenameOperations _componentFileRenameOperations;
    private readonly DocumentComponentInstanceRenameOperations _componentInstanceRenameOperations;

    public DocumentRenameOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentRenameOperations> logger,
        DocumentFileRenameOperations fileRenameOperations,
        DocumentComponentFileRenameOperations componentFileRenameOperations,
        DocumentComponentInstanceRenameOperations componentInstanceRenameOperations)
        : base(connection, settings, logger)
    {
        _fileRenameOperations = fileRenameOperations;
        _componentFileRenameOperations = componentFileRenameOperations;
        _componentInstanceRenameOperations = componentInstanceRenameOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            var op when op == DocumentGovernanceOperationNames.RenameDocument => _fileRenameOperations.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op == DocumentGovernanceOperationNames.RenameComponentFile => _componentFileRenameOperations.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op == DocumentGovernanceOperationNames.RenameComponentInstance => _componentInstanceRenameOperations.ExecuteAsync(operation, parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown document rename operation: {operation}"))
        };
    }
}
