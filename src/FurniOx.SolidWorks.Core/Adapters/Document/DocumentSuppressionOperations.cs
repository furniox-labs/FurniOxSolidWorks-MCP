using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Single-component suppression read/write on the active assembly. Wraps
/// IComponent2.GetSuppression / SetSuppression2 with name-based lookup so
/// callers can preserve and restore states across destructive flows like rename.
/// Batch read/set lives in DocumentSuppressionBatchOperations (private).
/// </summary>
public sealed class DocumentSuppressionOperations : OperationHandlerBase
{
    public DocumentSuppressionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentSuppressionOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            DocumentSuppressionOperationNames.GetComponentSuppression => GetComponentSuppressionAsync(parameters),
            DocumentSuppressionOperationNames.SetComponentSuppression => SetComponentSuppressionAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown suppression operation: {operation}"))
        };
    }

    private Task<ExecutionResult> GetComponentSuppressionAsync(IDictionary<string, object?> parameters)
    {
        if (!DocumentSuppressionShared.TryResolveAssemblyComponent(_connection, parameters, out var component, out var failure))
        {
            return Task.FromResult(failure!);
        }

        return Task.FromResult(ExecutionResult.SuccessResult(DocumentSuppressionShared.BuildComponentStateInfo(component!)));
    }

    private Task<ExecutionResult> SetComponentSuppressionAsync(IDictionary<string, object?> parameters)
    {
        if (!DocumentSuppressionShared.TryResolveAssemblyComponent(_connection, parameters, out var component, out var failure))
        {
            return Task.FromResult(failure!);
        }

        if (!DocumentSuppressionShared.TryParseStateParam(parameters, "State", out var targetState, out var stateError))
        {
            return Task.FromResult(ExecutionResult.Failure(stateError!));
        }

        var targetComponent = component!;
        var beforeState = targetComponent.ReadSuppressionState();
        var result = DocumentSuppressionShared.ApplySuppression(targetComponent, targetState);

        return Task.FromResult(result.Ok
            ? ExecutionResult.SuccessResult(new
            {
                ComponentName = targetComponent.Name2 ?? string.Empty,
                BeforeState = DocumentSuppressionShared.StateName(beforeState),
                BeforeStateCode = beforeState,
                AfterState = DocumentSuppressionShared.StateName(result.AfterState),
                AfterStateCode = result.AfterState,
                Changed = beforeState != result.AfterState
            })
            : ExecutionResult.Failure(result.Error!, new
            {
                ComponentName = targetComponent.Name2 ?? string.Empty,
                BeforeState = DocumentSuppressionShared.StateName(beforeState),
                BeforeStateCode = beforeState,
                RequestedState = DocumentSuppressionShared.StateName(targetState),
                RequestedStateCode = targetState,
                ActualState = DocumentSuppressionShared.StateName(result.AfterState),
                ActualStateCode = result.AfterState
            }));
    }
}
