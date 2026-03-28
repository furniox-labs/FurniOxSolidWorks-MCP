using System;
using System.Collections.Generic;
using System.Text.Json;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Shared.Models;
using ModelContextProtocol;

namespace FurniOx.SolidWorks.MCP.Tools;

/// <summary>
/// Base class for MCP tool categories with shared helper methods
/// </summary>
public abstract class ToolsBase
{
    protected readonly ISmartRouter Router;

    protected ToolsBase(ISmartRouter router)
    {
        Router = router;
    }

    protected Task<object?> ExecuteToolAsync(string operation)
        => ExecuteToolAsync(operation, new Dictionary<string, object?>());

    protected async Task<object?> ExecuteToolAsync(string operation, IDictionary<string, object?> parameters)
        => MapExecutionResult(await Router.RouteAsync(operation, parameters));

    protected static object? MapExecutionResult(ExecutionResult result)
    {
        if (!result.Success)
        {
            throw CreateToolException(result);
        }

        if (result.Data == null && string.IsNullOrWhiteSpace(result.Message))
        {
            return new ToolResultEnvelope(Success: true);
        }

        if (string.IsNullOrWhiteSpace(result.Message))
        {
            return result.Data;
        }

        return new ToolResultEnvelope(
            Success: true,
            Message: result.Message,
            Data: result.Data);
    }

    private static McpException CreateToolException(ExecutionResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.Message)
            ? "SolidWorks operation failed."
            : result.Message;

        if (result.Data == null)
        {
            return new McpException(message);
        }

        try
        {
            var details = JsonSerializer.Serialize(result.Data, JsonOptions);
            return new McpException($"{message}{Environment.NewLine}Details:{Environment.NewLine}{details}");
        }
        catch
        {
            return new McpException(message);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    protected sealed record ToolResultEnvelope(bool Success, string? Message = null, object? Data = null);
}
