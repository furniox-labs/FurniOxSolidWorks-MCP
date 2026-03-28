using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Features;

public sealed class FeatureShellOperations : OperationHandlerBase
{
    public FeatureShellOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<FeatureShellOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return Task.FromResult(ExecutionResult.Failure("No active document"));

        var modelExt = model.Extension;
        if (modelExt == null) return Task.FromResult(ExecutionResult.Failure("No model extension"));

        var thickness = GetDoubleParam(parameters, "Thickness", 3.0);
        var direction = GetIntParam(parameters, "Direction", 0);
        var faceNames = FeatureSupport.GetStringArrayParam(parameters, "FaceNames");

        if (thickness <= 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Thickness must be greater than 0"));
        }

        if (thickness < 0.5)
        {
            _logger.LogWarning("Thickness {Thickness}mm is very thin and may cause manufacturing issues", thickness);
        }

        if (thickness > 50)
        {
            _logger.LogWarning("Thickness {Thickness}mm is unusually large and may exceed geometry constraints", thickness);
        }

        if (direction < 0 || direction > 1)
        {
            return Task.FromResult(ExecutionResult.Failure("Direction must be 0 (Inward) or 1 (Outward)"));
        }

        if (faceNames == null || faceNames.Length == 0)
        {
            return Task.FromResult(ExecutionResult.Failure(
                "FaceNames required - at least one face must be selected for removal. Cannot shell the entire body with zero faces selected."));
        }

        var thicknessMeters = MmToMeters(thickness);
        var outward = direction == 1;

        _logger.LogInformation(
            "Creating shell: Thickness={ThicknessMm}mm ({ThicknessM}m), Direction={Direction}, Faces={FaceCount}",
            thickness, thicknessMeters, (ShellDirection)direction, faceNames.Length);

        try
        {
            model.ClearSelection2(true);

            for (var i = 0; i < faceNames.Length; i++)
            {
                var selected = modelExt.SelectByID2(faceNames[i], "FACE", 0, 0, 0, i > 0, 1, null, 0);
                if (!selected)
                {
                    return Task.FromResult(ExecutionResult.Failure(
                        $"Failed to select face: {faceNames[i]}. Verify face name exists in the model and is a valid face entity."));
                }
            }

            var selectionManager = (ISelectionMgr?)model.ISelectionManager;
            var selectedCount = selectionManager?.GetSelectedObjectCount2(-1) ?? 0;
            if (selectedCount == 0)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "No faces selected after selection loop. This indicates a selection workflow error."));
            }

            _logger.LogDebug("Selected {Count} face(s) for shell removal", selectedCount);

            model.InsertFeatureShell(thicknessMeters, outward);
            var shellFeature = (Feature?)modelExt.GetLastFeatureAdded();

            if (shellFeature == null)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "Failed to create shell feature. Common causes: thickness exceeds the minimum radius of curvature, geometry has sharp defects, face offsets intersect, or shell selection marks are wrong."));
            }

            if (string.IsNullOrEmpty(shellFeature.Name))
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "Shell feature was created but appears invalid (empty name). This may indicate geometry errors."));
            }

            var rebuildResult = model.ForceRebuild3(false);
            if (!rebuildResult)
            {
                _logger.LogWarning("Shell feature created but rebuild returned warnings. Feature may have errors.");
            }

            var errorCode = shellFeature.GetErrorCode();
            if (errorCode != 0)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    $"Shell feature created but has error code {errorCode}. The feature may be suppressed or have geometric issues."));
            }

            _logger.LogInformation(
                "Shell feature created successfully: {FeatureName}, Thickness={ThicknessMm}mm, Direction={Direction}",
                shellFeature.Name, thickness, (ShellDirection)direction);

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                FeatureName = shellFeature.Name,
                FeatureType = "Shell",
                Parameters = new
                {
                    Thickness = thickness,
                    ThicknessMeters = thicknessMeters,
                    Direction = ((ShellDirection)direction).ToString(),
                    DirectionValue = outward ? "Outward/Outside" : "Inward/Inside",
                    FaceCount = faceNames.Length,
                    FacesRemoved = faceNames
                }
            }));
        }
        finally
        {
            model.ClearSelection2(true);
        }
    }
}

