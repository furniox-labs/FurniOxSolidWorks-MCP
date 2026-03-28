using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchSpecialized;

internal sealed class SketchTextOperations : OperationHandlerBase
{
    public SketchTextOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchTextOperations> logger)
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
            "Sketch.SketchText" => SketchTextAsync(parameters),
            "Sketch.SketchTextOnPath" => Task.FromResult(ExecutionResult.Failure(
                "SketchTextOnPath not yet implemented - requires two-step API (InsertSketchText -> SetTextOnPath)")),
            "Sketch.SketchSymbol" => SketchSymbolAsync(),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch text operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SketchTextAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSpecializedContextSupport.TryGetActiveSketch(_connection, out _, out var model, out _, out _, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var text = GetStringParam(parameters, "Text", string.Empty);
        var charHeight = MmToMeters(GetDoubleParam(parameters, "CharHeight", 5.0));
        var charWidth = MmToMeters(GetDoubleParam(parameters, "CharWidth", 3.0));
        var angle = DegreesToRadians(GetDoubleParam(parameters, "Angle", 0.0));
        var fontName = GetStringParam(parameters, "FontName", "Arial");
        var flipX = GetBoolParam(parameters, "FlipX", false) ? 1 : 0;
        var flipY = GetBoolParam(parameters, "FlipY", false) ? 1 : 0;
        var obliqAngle = DegreesToRadians(GetDoubleParam(parameters, "ObliqAngle", 0.0));

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(ExecutionResult.Failure("Text parameter is required"));
        }

        var selectionMgr = (SelectionMgr?)model!.SelectionManager;
        var selectionCount = selectionMgr?.GetSelectedObjectCount2(-1) ?? 0;
        if (selectionCount == 0)
        {
            _logger.LogWarning("No sketch entity selected - InsertSketchText requires pre-selected baseline");
            return Task.FromResult(ExecutionResult.Failure("A sketch entity must be pre-selected as baseline for text"));
        }

        var sketchText = model.InsertSketchText(
            charHeight,
            charWidth,
            angle,
            fontName,
            flipX,
            flipY,
            0,
            (int)obliqAngle,
            0);

        if (sketchText == null)
        {
            _logger.LogWarning("InsertSketchText returned null for text '{Text}'", text);
            return Task.FromResult(ExecutionResult.Failure(
                "Failed to create sketch text. Note: Text content may need to be set via ISketchText.Text property after insertion."));
        }

        _logger.LogInformation("Created sketch text '{Text}' with font {Font}", text, fontName);

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Sketch text created successfully",
            ["text"] = text,
            ["font"] = fontName,
            ["charHeight_mm"] = charHeight * 1000,
            ["charWidth_mm"] = charWidth * 1000,
            ["angle_deg"] = RadiansToDegrees(angle)
        }));
    }

    private Task<ExecutionResult> SketchSymbolAsync()
    {
        _logger.LogWarning("SketchSymbol operation attempted - feature is obsolete");

        return Task.FromResult(ExecutionResult.Failure(
            "SketchSymbol is OBSOLETE in SolidWorks 2023. The .sldsym format has been replaced by .sldblk (sketch blocks). Please use the InsertBlock operation instead. Note: 'Sketch symbol' terminology is from Autodesk Inventor; SolidWorks uses 'sketch blocks' for reusable sketch geometry."));
    }
}
