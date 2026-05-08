using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class EquationReferenceOperations : OperationHandlerBase
{
    public EquationReferenceOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<EquationReferenceOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (operation != EquationOperationNames.ScanReferencesBatch
            && operation != EquationOperationNames.RepairReferencesBatch)
        {
            return Task.FromResult(ExecutionResult.Failure($"Unknown equation reference operation: {operation}"));
        }

        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var runOptions = new EquationReferenceBatchRunOptions
        {
            InputPath = GetStringParamNullable(parameters, "InputPath"),
            OutputPath = GetStringParamNullable(parameters, "OutputPath"),
            DocumentPath = GetStringParamNullable(parameters, "DocumentPath"),
            DryRun = operation == EquationOperationNames.ScanReferencesBatch || GetBoolParam(parameters, "DryRun", true),
            SaveDocuments = GetBoolParam(parameters, "SaveDocuments", false),
            AllConfigurations = GetBoolParam(parameters, "AllConfigurations", false),
            OpenUnloadedDocuments = GetBoolParam(parameters, "OpenUnloadedDocuments", false),
            CloseOpened = GetBoolParam(parameters, "CloseOpened", true),
            HiddenInGui = GetBoolParam(parameters, "HiddenInGui", true),
            IncludeActiveDocument = parameters.ContainsKey("IncludeActiveDocument")
                ? GetBoolParam(parameters, "IncludeActiveDocument", true)
                : null,
            UseActiveAssemblyComponents = parameters.ContainsKey("UseActiveAssemblyComponents")
                ? GetBoolParam(parameters, "UseActiveAssemblyComponents", true)
                : null
        };

        var result = EquationReferenceBatchRunner.Execute(app, runOptions);
        return Task.FromResult(!string.IsNullOrWhiteSpace(result.Error)
            ? ExecutionResult.Failure(result.Error, result)
            : ExecutionResult.SuccessResult(result, operation == EquationOperationNames.ScanReferencesBatch
                ? "Equation reference scan completed"
                : runOptions.DryRun
                    ? "Equation reference repair dry-run completed"
                    : "Equation reference repair completed"));
    }
}
