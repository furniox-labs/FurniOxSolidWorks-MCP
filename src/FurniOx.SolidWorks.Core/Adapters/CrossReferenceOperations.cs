using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class CrossReferenceOperations : OperationHandlerBase
{
    public CrossReferenceOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<CrossReferenceOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (!CrossReferenceOperationNames.All.Contains(operation))
        {
            return Task.FromResult(ExecutionResult.Failure($"Unknown cross-reference operation: {operation}"));
        }

        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var quickMode = GetBoolParam(parameters, "QuickMode", false);
        var runOptions = new CrossReferenceBatchRunOptions
        {
            InputPath = GetStringParamNullable(parameters, "InputPath"),
            OutputPath = GetStringParamNullable(parameters, "OutputPath"),
            DocumentPath = GetStringParamNullable(parameters, "DocumentPath"),
            TargetScope = GetTargetScope(operation),
            ComponentName = GetStringParamNullable(parameters, "ComponentName"),
            FeatureName = GetStringParamNullable(parameters, "FeatureName") ?? GetStringParamNullable(parameters, "SketchName"),
            IncludeExternalFileReferences = GetBoolParam(parameters, "IncludeExternalFileReferences", true),
            IncludeAuxiliaryReferences = GetBoolParam(parameters, "IncludeAuxiliaryReferences", true),
            IncludeDrawingReferences = quickMode ? false : GetBoolParam(parameters, "IncludeDrawingReferences", true),
            IncludeEquationReferences = quickMode ? false : GetBoolParam(parameters, "IncludeEquationReferences", true),
            AllConfigurations = GetBoolParam(parameters, "AllConfigurations", false),
            OpenUnloadedDocuments = GetBoolParam(parameters, "OpenUnloadedDocuments", false),
            CloseOpened = GetBoolParam(parameters, "CloseOpened", true),
            HiddenInGui = GetBoolParam(parameters, "HiddenInGui", true),
            LightWeightOpen = GetBoolParam(parameters, "LightWeightOpen", true),
            DontLoadHiddenComponents = GetBoolParam(parameters, "DontLoadHiddenComponents", true),
            QuickMode = quickMode,
            MaxDocOpenTimeMs = GetIntParam(parameters, "MaxDocOpenTimeMs", 0),
            BatchSize = GetIntParam(parameters, "BatchSize", 20),
            IncludeActiveDocument = parameters.ContainsKey("IncludeActiveDocument")
                ? GetBoolParam(parameters, "IncludeActiveDocument", true)
                : null,
            UseActiveAssemblyComponents = parameters.ContainsKey("UseActiveAssemblyComponents")
                ? GetBoolParam(parameters, "UseActiveAssemblyComponents", true)
                : null,
            IncludeOpenDocuments = parameters.ContainsKey("IncludeOpenDocuments")
                ? GetBoolParam(parameters, "IncludeOpenDocuments", false)
                : null
        };

        var result = CrossReferenceBatchRunner.Execute(app, runOptions);
        return Task.FromResult(!string.IsNullOrWhiteSpace(result.Error)
            ? ExecutionResult.Failure(result.Error, result)
            : ExecutionResult.SuccessResult(result, operation == CrossReferenceOperationNames.VerifyNoBrokenReferencesSingle
                ? result.Passed
                    ? "No broken cross-references detected"
                    : "Broken cross-references detected"
                : "Cross-reference scan completed"));
    }

    private static string GetTargetScope(string operation)
    {
        if (operation == CrossReferenceOperationNames.ScanComponentExternalReferences)
        {
            return "Component";
        }
        if (operation == CrossReferenceOperationNames.ScanFeatureExternalReferences)
        {
            return "Feature";
        }
        if (operation == CrossReferenceOperationNames.ScanSketchExternalReferences)
        {
            return "Sketch";
        }

        return "Documents";
    }
}
