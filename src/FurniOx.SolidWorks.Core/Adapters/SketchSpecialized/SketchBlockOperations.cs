using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.SketchSpecialized;

internal sealed class SketchBlockOperations : OperationHandlerBase
{
    public SketchBlockOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SketchBlockOperations> logger)
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
            "Sketch.InsertBlock" => InsertBlockAsync(parameters),
            "Sketch.MakeBlock" => MakeBlockAsync(parameters),
            "Sketch.ExplodeBlock" => ExplodeBlockAsync(parameters),
            "Sketch.SaveBlock" => Task.FromResult(ExecutionResult.Failure(
                "SaveBlock is NOT AVAILABLE in SolidWorks 2023 API. The API provides no methods to save block definitions to .sldblk files. Workaround: Create .sldblk files manually through SolidWorks UI (Tools -> Block -> Save), then load them programmatically using InsertBlock operation.")),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown sketch block operation: {operation}"))
        };
    }

    private Task<ExecutionResult> InsertBlockAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSpecializedContextSupport.TryGetActiveSketch(_connection, out var app, out var model, out var sketchManager, out _, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var blockPath = GetStringParam(parameters, "BlockPath", string.Empty);
        var x = MmToMeters(GetDoubleParam(parameters, "X", 0.0));
        var y = MmToMeters(GetDoubleParam(parameters, "Y", 0.0));
        var z = MmToMeters(GetDoubleParam(parameters, "Z", 0.0));
        var scale = GetDoubleParam(parameters, "Scale", 1.0);
        var rotation = DegreesToRadians(GetDoubleParam(parameters, "Rotation", 0.0));

        if (string.IsNullOrEmpty(blockPath))
        {
            return Task.FromResult(ExecutionResult.Failure("BlockPath parameter is required"));
        }

        if (!File.Exists(blockPath))
        {
            return Task.FromResult(ExecutionResult.Failure($"Block file not found: {blockPath}"));
        }

        var mathUtil = (IMathUtility?)app!.GetMathUtility();
        if (mathUtil == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get math utility"));
        }

        var position = (IMathPoint?)mathUtil.CreatePoint(new[] { x, y, z });
        if (position == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to create position point"));
        }

        var blockInstance = sketchManager!.MakeSketchBlockFromFile((MathPoint)position, blockPath, false, scale, rotation);
        if (blockInstance == null)
        {
            _logger.LogWarning("MakeSketchBlockFromFile returned null for {BlockPath}", blockPath);
            return Task.FromResult(ExecutionResult.Failure("Failed to insert block"));
        }

        _logger.LogInformation("Inserted block from {BlockPath} at ({X},{Y})", blockPath, x * 1000, y * 1000);

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "Block inserted successfully",
            ["blockPath"] = blockPath,
            ["x_mm"] = x * 1000,
            ["y_mm"] = y * 1000,
            ["scale"] = scale,
            ["rotation_deg"] = RadiansToDegrees(rotation),
            ["note"] = "Position/scale/rotation applied via file insertion"
        }));
    }

    private Task<ExecutionResult> MakeBlockAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSpecializedContextSupport.TryGetActiveSketch(_connection, out var app, out var model, out var sketchManager, out _, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var selectionMgr = (SelectionMgr?)model!.SelectionManager;
        var selectionCount = selectionMgr?.GetSelectedObjectCount2(-1) ?? 0;
        if (selectionCount == 0)
        {
            _logger.LogWarning("No entities selected - MakeBlock requires pre-selected entities");
            return Task.FromResult(ExecutionResult.Failure("Entities must be pre-selected to create block"));
        }

        try
        {
            var mathUtil = (IMathUtility?)app!.GetMathUtility();
            if (mathUtil == null)
            {
                return Task.FromResult(ExecutionResult.Failure("Failed to get math utility"));
            }

            var insertionPoint = (IMathPoint?)mathUtil.CreatePoint(new[] { 0d, 0d, 0d });
            if (insertionPoint == null)
            {
                return Task.FromResult(ExecutionResult.Failure("Failed to create insertion point"));
            }

            var blockDefinition = sketchManager!.MakeSketchBlockFromSelected((MathPoint)insertionPoint);
            if (blockDefinition == null)
            {
                _logger.LogWarning("MakeSketchBlockFromSelected returned null");
                return Task.FromResult(ExecutionResult.Failure("Failed to create block from selection"));
            }

            model.ClearSelection2(true);
            _logger.LogInformation("Created block definition from {Count} entities", selectionCount);

            return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
            {
                ["message"] = "Block created successfully",
                ["entityCount"] = selectionCount,
                ["note"] = "Using MakeSketchBlockFromSelected API (availability uncertain)"
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MakeBlock failed - API may not exist in this SolidWorks version");
            return Task.FromResult(ExecutionResult.Failure(
                $"MakeBlock API not available in this SolidWorks version: {ex.Message}"));
        }
    }

    private Task<ExecutionResult> ExplodeBlockAsync(IDictionary<string, object?> parameters)
    {
        if (!SketchSpecializedContextSupport.TryGetActiveSketch(_connection, out _, out _, out var sketchManager, out var activeSketch, out var errorMessage))
        {
            return Task.FromResult(ExecutionResult.Failure(errorMessage ?? "No active sketch"));
        }

        var blockIndex = GetIntParam(parameters, "BlockIndex", -1);
        var blockCount = activeSketch!.GetSketchBlockInstanceCount();
        if (blockCount == 0)
        {
            return Task.FromResult(ExecutionResult.Failure("No blocks in active sketch"));
        }

        var blocksObject = activeSketch.GetSketchBlockInstances();
        if (blocksObject == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Failed to get block instances"));
        }

        var blocks = SketchSpecializedContextSupport.GetObjectArrayOrEmpty(blocksObject);
        if (blockIndex >= 0)
        {
            if (blockIndex >= blocks.Length)
            {
                return Task.FromResult(ExecutionResult.Failure($"BlockIndex {blockIndex} out of range (0-{blocks.Length - 1})"));
            }

            if (blocks[blockIndex] is not SketchBlockInstance blockInstance)
            {
                return Task.FromResult(ExecutionResult.Failure($"Invalid block at index {blockIndex}"));
            }

            sketchManager!.ExplodeSketchBlockInstance(blockInstance);
            _logger.LogInformation("Exploded block at index {Index}", blockIndex);

            return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
            {
                ["message"] = "Block exploded successfully",
                ["blockIndex"] = blockIndex,
                ["totalBlocks"] = blocks.Length
            }));
        }

        var explodedCount = 0;
        for (var index = blocks.Length - 1; index >= 0; index--)
        {
            if (blocks[index] is not SketchBlockInstance blockInstance)
            {
                continue;
            }

            sketchManager!.ExplodeSketchBlockInstance(blockInstance);
            explodedCount++;
        }

        _logger.LogInformation("Exploded {Count} blocks from active sketch", explodedCount);

        return Task.FromResult(ExecutionResult.SuccessResult(new Dictionary<string, object?>
        {
            ["message"] = "All blocks exploded successfully",
            ["explodedCount"] = explodedCount,
            ["totalBlocks"] = blocks.Length
        }));
    }
}
