using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchGeometry;

internal sealed class SketchSessionGeometryOperations : OperationHandlerBase
{
    public SketchSessionGeometryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchSessionGeometryOperations> logger)
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
            "Sketch.CreateSketch" => CreateSketchAsync(parameters),
            "Sketch.EditSketch" => Task.FromResult(EditSketch(parameters)),
            "Sketch.ExitSketch" => ExitSketchAsync(),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch session operation: {operation}"))
        };
    }

    private Task<ExecutionResult> CreateSketchAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        var plane = GetStringParam(parameters, "Plane", "Front");
        model!.Extension.SelectByID2($"{plane} Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
        model.SketchManager.InsertSketch(true);

        return Task.FromResult(ExecutionResult.SuccessResult(new { Plane = plane }));
    }

    private Task<ExecutionResult> ExitSketchAsync()
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active document"));
        }

        model!.SketchManager.InsertSketch(true);
        return Task.FromResult(ExecutionResult.SuccessResult(new { Exited = true }));
    }

    private ExecutionResult EditSketch(IDictionary<string, object?> parameters)
    {
        if (!SketchGeometryContextSupport.TryGetModel(_connection, out var model, out var errorMessage))
        {
            return ExecutionResult.Failure(errorMessage ?? "No active document");
        }

        var sketchName = GetStringParam(parameters, "SketchName", string.Empty);
        var useSelected = GetBoolParam(parameters, "UseSelected", false);

        ISketch? sketch = null;
        IFeature? sketchFeature = null;
        string resolvedName;

        if (useSelected || string.IsNullOrEmpty(sketchName))
        {
            var selectionManager = model!.SelectionManager as ISelectionMgr;
            if (selectionManager == null || selectionManager.GetSelectedObjectCount2(-1) == 0)
            {
                return ExecutionResult.Failure("No sketch selected and no SketchName provided. Select a sketch or provide SketchName.");
            }

            var selectedObject = selectionManager.GetSelectedObject6(1, -1);
            if (selectedObject is not IFeature selectedFeature)
            {
                return ExecutionResult.Failure("Selected object is not a feature");
            }

            sketch = selectedFeature.GetSpecificFeature2() as ISketch;
            sketchFeature = selectedFeature;
            resolvedName = selectedFeature.Name;
        }
        else
        {
            sketchFeature = SketchGeometryContextSupport.FindFeatureByName(model!, sketchName);
            if (sketchFeature == null)
            {
                return ExecutionResult.Failure($"Sketch '{sketchName}' not found in feature tree");
            }

            sketch = sketchFeature.GetSpecificFeature2() as ISketch;
            resolvedName = sketchName;

            if (!sketchFeature.Select2(false, 0))
            {
                return ExecutionResult.Failure($"Failed to select sketch '{sketchName}' using Select2");
            }
        }

        if (sketch == null)
        {
            return ExecutionResult.Failure("Failed to get sketch object. Make sure a sketch is selected or provide a valid SketchName.");
        }

        var is3D = sketch.Is3D();
        model!.EditSketch();

        return ExecutionResult.SuccessResult(new
        {
            SketchName = resolvedName,
            EditMode = true,
            Is3D = is3D
        }, $"Entered edit mode for sketch '{resolvedName}'");
    }
}
