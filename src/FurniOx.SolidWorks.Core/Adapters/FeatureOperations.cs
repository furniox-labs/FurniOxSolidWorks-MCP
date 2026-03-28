using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Features;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class FeatureOperations : OperationHandlerBase
{
    private readonly FeatureBossExtrusionOperations _bossExtrusionOperations;
    private readonly FeatureCutExtrusionOperations _cutExtrusionOperations;
    private readonly FeatureRevolveOperations _revolveOperations;
    private readonly FeatureFilletOperations _filletOperations;
    private readonly FeatureShellOperations _shellOperations;

    public FeatureOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<FeatureOperations> logger,
        FeatureBossExtrusionOperations bossExtrusionOperations,
        FeatureCutExtrusionOperations cutExtrusionOperations,
        FeatureRevolveOperations revolveOperations,
        FeatureFilletOperations filletOperations,
        FeatureShellOperations shellOperations)
        : base(connection, settings, logger)
    {
        _bossExtrusionOperations = bossExtrusionOperations;
        _cutExtrusionOperations = cutExtrusionOperations;
        _revolveOperations = revolveOperations;
        _filletOperations = filletOperations;
        _shellOperations = shellOperations;
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (FeatureOperationNames.Extrusion.Contains(operation))
        {
            return operation == FeatureOperationNames.CreateExtrusion
                ? _bossExtrusionOperations.ExecuteAsync(operation, parameters, cancellationToken)
                : _cutExtrusionOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        if (FeatureOperationNames.Revolve.Contains(operation))
        {
            return _revolveOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        if (FeatureOperationNames.Fillet.Contains(operation))
        {
            return _filletOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        if (FeatureOperationNames.Shell.Contains(operation))
        {
            return _shellOperations.ExecuteAsync(operation, parameters, cancellationToken);
        }

        return Task.FromResult(ExecutionResult.Failure($"Unknown feature operation: {operation}"));
    }
}

