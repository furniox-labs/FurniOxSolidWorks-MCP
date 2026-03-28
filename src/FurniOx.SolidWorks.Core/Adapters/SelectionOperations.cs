using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Handles all selection-related operations (3 operations)
/// </summary>
public class SelectionOperations : OperationHandlerBase
{
    public SelectionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SelectionOperations> logger)
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
            "Selection.SelectByID2" => SelectByID2Async(parameters, cancellationToken),
            "Selection.SelectComponent" => SelectComponentAsync(parameters, cancellationToken),
            "Selection.ClearSelection2" => ClearSelection2Async(cancellationToken),
            "Selection.DeleteSelection2" => DeleteSelection2Async(parameters, cancellationToken),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown selection operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SelectByID2Async(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
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

        // Extract required parameters
        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string name)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter"));
        }

        if (!parameters.TryGetValue("Type", out var typeObj) || typeObj is not string type)
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Type' parameter"));
        }

        // Extract optional coordinates (default to 0,0,0)
        var x = GetDoubleParam(parameters, "X");
        var y = GetDoubleParam(parameters, "Y");
        var z = GetDoubleParam(parameters, "Z");

        // Extract optional parameters
        var append = GetBoolParam(parameters, "Append"); // Add to selection vs replace
        var mark = GetIntParam(parameters, "Mark");       // Selection mark for identifying selections
        var selectOption = GetIntParam(parameters, "SelectOption"); // Selection options

        // SelectByID2 signature:
        // bool SelectByID2(string Name, string Type, double X, double Y, double Z,
        //                  bool Append, int Mark, Callout Callout, int SelectOption)
        bool result = model.Extension.SelectByID2(
            name,
            type,
            x, y, z,
            append,
            mark,
            null,  // Callout (typically null)
            selectOption);

        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to select entity '{name}' of type '{type}'"));
        }

        // Get selection count to verify
        var selMgr = (ISelectionMgr?)model.SelectionManager;
        int selectionCount = selMgr?.GetSelectedObjectCount2(-1) ?? 0;

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

    /// <summary>
    /// Select a component using hierarchical path traversal + Select4.
    /// Supports both direct children (e.g., "Part1-1") and nested components (e.g., "SubAssy-1/Part-1").
    /// For nested paths, traverses the hierarchy using FeatureByName at each level.
    /// </summary>
    private Task<ExecutionResult> SelectComponentAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
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

        // Verify it's an assembly
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return Task.FromResult(ExecutionResult.Failure("Active document must be an assembly to select components"));
        }

        var assembly = (IAssemblyDoc)model;

        // Get component name from parameters
        if (!parameters.TryGetValue("Name", out var nameObj) || nameObj is not string componentName || string.IsNullOrEmpty(componentName))
        {
            return Task.FromResult(ExecutionResult.Failure("Missing or invalid 'Name' parameter. Provide the component name (e.g., 'Part1-1' from analyze_assembly)."));
        }

        // Extract optional parameters
        var append = GetBoolParam(parameters, "Append"); // Add to selection vs replace
        var mark = GetIntParam(parameters, "Mark");       // Selection mark for identifying selections

        // Try to find the component - supports both direct and nested paths
        IComponent2? component = null;
        string method = "";

        if (componentName.Contains('/'))
        {
            // NESTED PATH: Traverse hierarchy using FeatureByName
            // Path format: "SubAssy-1/Part-1" or "Level1-1/Level2-1/Part-1"
            component = GetNestedComponentByPath(assembly, model, componentName);
            method = "Hierarchical path traversal + Select4";
        }
        else
        {
            // DIRECT CHILD: Use GetComponentByName (faster for direct children)
            component = (IComponent2?)assembly.GetComponentByName(componentName);
            method = "GetComponentByName + Select4";
        }

        if (component == null)
        {
            // Try to help the user by listing available components (including nested)
            var availableComponents = new List<string>();
            // SolidWorks API: GetComponents(true) returns top-level only; GetComponents(false) returns all components (includes nested)
            var components = assembly.GetComponents(false).ToObjectArraySafe();
            if (components != null)
            {
                foreach (var compObj in components)
                {
                    if (compObj is IComponent2 comp)
                    {
                        availableComponents.Add(comp.Name2 ?? "");
                    }
                }
            }

            return Task.FromResult(ExecutionResult.Failure($"Component '{componentName}' not found in assembly", new
            {
                RequestedName = componentName,
                AvailableComponents = availableComponents.Take(30).ToList(), // First 30 for context
                TotalComponentCount = availableComponents.Count,
                Hint = "Use the Name field from analyze_assembly. For nested components use path format: 'SubAssy-1/Part-1'"
            }));
        }

        // Create selection data for mark if needed
        var selMgr = (ISelectionMgr?)model.SelectionManager;
        SelectData? selectData = null;

        if (selMgr != null && mark != 0)
        {
            selectData = (SelectData?)selMgr.CreateSelectData();
            if (selectData != null)
            {
                selectData.Mark = mark;
            }
        }

        // Use Select4 for robust selection
        // Signature: Select4(Append As Boolean, SelectData As SelectData, BubbleToParent As Boolean) As Boolean
        // BubbleToParent = false means select this component, not its parent in nested assemblies
        bool selected = component.Select4(append, selectData, false);

        if (!selected)
        {
            return Task.FromResult(ExecutionResult.Failure($"Failed to select component '{componentName}'. Component exists but Select4 failed."));
        }

        // Get selection count to verify
        int selectionCount = selMgr?.GetSelectedObjectCount2(-1) ?? 0;

        // Get additional component info
        var componentPath = component.GetPathName();
        var isVirtual = component.IsVirtual;
        var isSuppressed = component.IsSuppressed();

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Selected = true,
            ComponentName = componentName,
            ComponentPath = componentPath,
            SelectionCount = selectionCount,
            Appended = append,
            Mark = mark,
            IsVirtual = isVirtual,
            IsSuppressed = isSuppressed,
            Method = method
        }));
    }

    /// <summary>
    /// Get a nested component by traversing the hierarchy using FeatureByName.
    /// Path format: "SubAssy-1/Part-1" or "Level1-1/Level2-1/Part-1"
    /// Algorithm: Split path by "/", traverse each level using FeatureByName + GetSpecificFeature2
    /// </summary>
    private IComponent2? GetNestedComponentByPath(IAssemblyDoc assembly, ModelDoc2 model, string path)
    {
        try
        {
            var pathParts = path.Split('/');
            IComponent2? currentComponent = null;

            for (int i = 0; i < pathParts.Length; i++)
            {
                var partName = pathParts[i];
                IFeature? feature = null;

                if (i == 0)
                {
                    // First level: get feature from assembly
                    feature = (IFeature?)assembly.FeatureByName(partName);
                }
                else
                {
                    // Subsequent levels: get feature from current component
                    feature = (IFeature?)currentComponent?.FeatureByName(partName);
                }

                if (feature == null)
                {
                    _logger.LogWarning("Failed to find component at path level {Level}: '{Name}' in path '{Path}'",
                        i, partName, path);
                    return null;
                }

                // Get the component from the feature
                currentComponent = (IComponent2?)feature.GetSpecificFeature2();
                if (currentComponent == null)
                {
                    _logger.LogWarning("Failed to get component from feature at level {Level}: '{Name}'",
                        i, partName);
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

    private Task<ExecutionResult> ClearSelection2Async(CancellationToken cancellationToken)
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

        // Get selection manager
        var selMgr = (ISelectionMgr?)model.SelectionManager;
        if (selMgr == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get selection manager"));
        }

        // Get count before clearing
        int countBefore = selMgr.GetSelectedObjectCount2(-1);

        // ClearSelection2 clears all selections (void return)
        // Parameter: ClearFaces (true = also clear face selections)
        model.ClearSelection2(true);

        // Verify cleared
        int countAfter = selMgr.GetSelectedObjectCount2(-1);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Cleared = true,
            ItemsCleared = countBefore,
            RemainingSelections = countAfter
        }));
    }

    private Task<ExecutionResult> DeleteSelection2Async(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
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

        // Get selection manager
        var selMgr = (ISelectionMgr?)model.SelectionManager;
        if (selMgr == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get selection manager"));
        }

        // Get count before deletion
        int countBefore = selMgr.GetSelectedObjectCount2(-1);

        if (countBefore == 0)
        {
            return Task.FromResult(ExecutionResult.Failure("No entities selected to delete"));
        }

        // Get optional delete options parameter
        var options = GetIntParam(parameters, "Options", (int)swDeleteSelectionOptions_e.swDelete_Absorbed);

        // DeleteSelection2 deletes selected entities (returns bool, not int)
        // options: swDelete_Absorbed, swDelete_Children, swDelete_Absorbed | swDelete_Children
        bool result = model.Extension.DeleteSelection2(options);

        // Result: true = success, false = failure
        if (!result)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to delete selection"));
        }

        // Get count after deletion
        int countAfter = selMgr.GetSelectedObjectCount2(-1);

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Deleted = true,
            ItemsDeleted = countBefore,
            RemainingSelections = countAfter,
            Options = options
        }));
    }
}
