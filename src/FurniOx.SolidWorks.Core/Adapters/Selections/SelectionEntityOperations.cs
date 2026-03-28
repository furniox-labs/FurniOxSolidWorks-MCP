using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Selections;

public sealed class SelectionEntityOperations : OperationHandlerBase
{
    public SelectionEntityOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SelectionEntityOperations> logger)
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
            "Selection.SelectByID2" => SelectByIdAsync(parameters),
            "Selection.ClearSelection2" => ClearSelectionAsync(),
            "Selection.DeleteSelection2" => DeleteSelectionAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown selection entity operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SelectByIdAsync(IDictionary<string, object?> parameters)
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string name)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        if (!parameters.TryGetValue("Type", out var typeObj) || typeObj is not string type)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Type' parameter"));
        }

        var x = GetDoubleParam(parameters, "X");
        var y = GetDoubleParam(parameters, "Y");
        var z = GetDoubleParam(parameters, "Z");
        var append = GetBoolParam(parameters, "Append");
        var mark = GetIntParam(parameters, "Mark");
        var selectOption = GetIntParam(parameters, "SelectOption");

        var result = model.Extension.SelectByID2(
            name,
            type,
            x,
            y,
            z,
            append,
            mark,
            null,
            selectOption);

        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to select entity '{name}' of type '{type}'"));
        }

        var selectionCount = ((ISelectionMgr?)model.SelectionManager)?.GetSelectedObjectCount2(-1) ?? 0;

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Selected = true,
            Name = name,
            Type = type,
            SelectionCount = selectionCount,
            Appended = append,
            Mark = mark
        }));
    }

    private Task<ExecutionResult> ClearSelectionAsync()
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        var selectionManager = (ISelectionMgr?)model.SelectionManager;
        if (selectionManager == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get selection manager"));
        }

        var countBefore = selectionManager.GetSelectedObjectCount2(-1);
        model.ClearSelection2(true);
        var countAfter = selectionManager.GetSelectedObjectCount2(-1);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Cleared = true,
            ItemsCleared = countBefore,
            RemainingSelections = countAfter
        }));
    }

    private Task<ExecutionResult> DeleteSelectionAsync(IDictionary<string, object?> parameters)
    {
        var model = GetActiveModel();
        if (model is null)
        {
            return Task.FromResult(NotConnectedOrNoDocument());
        }

        var selectionManager = (ISelectionMgr?)model.SelectionManager;
        if (selectionManager == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get selection manager"));
        }

        var countBefore = selectionManager.GetSelectedObjectCount2(-1);
        if (countBefore == 0)
        {
            return Task.FromResult(ExecutionResult.Failure("No entities selected to delete"));
        }

        var options = GetIntParam(parameters, "Options", (int)swDeleteSelectionOptions_e.swDelete_Absorbed);
        var result = model.Extension.DeleteSelection2(options);
        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to delete selection"));
        }

        var countAfter = selectionManager.GetSelectedObjectCount2(-1);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Deleted = true,
            ItemsDeleted = countBefore,
            RemainingSelections = countAfter,
            Options = options
        }));
    }

    private ModelDoc2? GetActiveModel()
    {
        return (ModelDoc2?)_connection.Application?.ActiveDoc;
    }

    private ExecutionResult NotConnectedOrNoDocument()
    {
        return _connection.Application == null
            ? ExecutionResult.Failure("Not connected to SolidWorks")
            : ExecutionResult.Failure("No active document");
    }
}
