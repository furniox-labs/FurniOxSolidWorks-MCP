using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Selections;

public sealed class SelectionComponentOperations : OperationHandlerBase
{
    public SelectionComponentOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SelectionComponentOperations> logger)
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
            "Selection.SelectComponent" => SelectComponentAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown selection component operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SelectComponentAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return Task.FromResult(ExecutionResult.Failure("Active document must be an assembly to select components"));
        }

        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string componentName || string.IsNullOrEmpty(componentName))
        {
            return Task.FromResult(ExecutionResult.Failure(
                "Missing or invalid 'Name' parameter. Use list_assembly_components to discover component names."));
        }

        var assembly = (IAssemblyDoc)model;
        var append = GetBoolParam(parameters, "Append");
        var mark = GetIntParam(parameters, "Mark");

        var component = componentName.Contains('/', StringComparison.Ordinal)
            ? GetNestedComponentByPath(assembly, componentName)
            : (IComponent2?)assembly.GetComponentByName(componentName);
        var method = componentName.Contains('/', StringComparison.Ordinal)
            ? "Hierarchical path traversal + Select4"
            : "GetComponentByName + Select4";

        if (component == null)
        {
            return Task.FromResult(ComponentNotFound(componentName, assembly));
        }

        var selectionManager = (ISelectionMgr?)model.SelectionManager;
        SelectData? selectData = null;

        if (selectionManager != null && mark != 0)
        {
            selectData = (SelectData?)selectionManager.CreateSelectData();
            if (selectData != null)
            {
                selectData.Mark = mark;
            }
        }

        var selected = component.Select4(append, selectData, false);
        if (!selected)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"Failed to select component '{componentName}'. Component exists but Select4 failed."));
        }

        var selectionCount = selectionManager?.GetSelectedObjectCount2(-1) ?? 0;

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Selected = true,
            ComponentName = componentName,
            ComponentPath = component.GetPathName(),
            SelectionCount = selectionCount,
            Appended = append,
            Mark = mark,
            IsVirtual = component.IsVirtual,
            IsSuppressed = component.IsSuppressed(),
            Method = method
        }));
    }

    private ExecutionResult ComponentNotFound(string componentName, IAssemblyDoc assembly)
    {
        var availableComponents = new List<string>();
        var components = assembly.GetComponents(false).ToObjectArraySafe();
        if (components != null)
        {
            foreach (var componentObj in components)
            {
                if (componentObj is IComponent2 component)
                {
                    availableComponents.Add(component.Name2 ?? string.Empty);
                }
            }
        }

        return ExecutionResult.Failure($"Component '{componentName}' not found in assembly", new
        {
            RequestedName = componentName,
            AvailableComponents = availableComponents.Take(30).ToList(),
            TotalComponentCount = availableComponents.Count,
            Hint = "Use list_assembly_components to get component names. For nested components use path format: 'SubAssy-1/Part-1'."
        });
    }

    private IComponent2? GetNestedComponentByPath(IAssemblyDoc assembly, string path)
    {
        try
        {
            var pathParts = path.Split('/');
            IComponent2? currentComponent = null;

            for (var i = 0; i < pathParts.Length; i++)
            {
                var partName = pathParts[i];
                var feature = i == 0
                    ? (IFeature?)assembly.FeatureByName(partName)
                    : (IFeature?)currentComponent?.FeatureByName(partName);

                if (feature == null)
                {
                    _logger.LogWarning(
                        "Failed to find component at path level {Level}: '{Name}' in path '{Path}'",
                        i,
                        partName,
                        path);
                    return null;
                }

                currentComponent = (IComponent2?)feature.GetSpecificFeature2();
                if (currentComponent == null)
                {
                    _logger.LogWarning(
                        "Failed to get component from feature at level {Level}: '{Name}'",
                        i,
                        partName);
                    return null;
                }
            }

            return currentComponent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error traversing component path: {Path}", path);
            return null;
        }
    }
}
