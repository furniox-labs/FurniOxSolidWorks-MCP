using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Sorting;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class SortingOperations : OperationHandlerBase
{
    private readonly SortingComponentReorderOperations _componentReorderOperations;
    private readonly SortingFeatureReorderOperations _featureReorderOperations;
    private readonly SortingInspectionOperations _inspectionOperations;

    public SortingOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SortingOperations> logger,
        SortingComponentReorderOperations componentReorderOperations,
        SortingFeatureReorderOperations featureReorderOperations,
        SortingInspectionOperations inspectionOperations)
        : base(connection, settings, logger)
    {
        _componentReorderOperations = componentReorderOperations;
        _featureReorderOperations = featureReorderOperations;
        _inspectionOperations = inspectionOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            var op when op == SortingOperationNames.ReorderByPositions => _componentReorderOperations.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op == SortingOperationNames.ReorderFeaturesByPositions => _featureReorderOperations.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op == SortingOperationNames.ListComponentFolders => _inspectionOperations.ExecuteAsync(operation, parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sorting operation: {operation}"))
        };
    }
}
