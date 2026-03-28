using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Base class for operation handlers providing shared dependencies and helper methods
/// </summary>
public abstract class OperationHandlerBase
{
    protected readonly SolidWorksConnection _connection;
    protected readonly SolidWorksSettings _settings;
    protected readonly ILogger _logger;

    /// <summary>
    /// Shared JSON serializer options for analysis output files. Reused across all callers to
    /// avoid per-call allocations (JsonSerializerOptions is expensive to construct).
    /// </summary>
    protected static readonly JsonSerializerOptions AnalysisJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Shared JSON serializer options for analysis output that requires camelCase property names.
    /// </summary>
    protected static readonly JsonSerializerOptions AnalysisCamelCaseJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected OperationHandlerBase(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute an operation asynchronously
    /// </summary>
    public abstract Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken);

    #region Parameter Extraction Helpers

    /// <summary>
    /// Extract double parameter with default value
    /// </summary>
    protected static double GetDoubleParam(IDictionary<string, object?> parameters, string key, double defaultValue = 0.0)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        // Handle different types from MCP deserialization
        if (value is double d)
        {
            return d;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
        {
            return jsonElement.GetDouble();
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return l;
        }

        return defaultValue;
    }

    /// <summary>
    /// Extract integer parameter with default value
    /// </summary>
    protected static int GetIntParam(IDictionary<string, object?> parameters, string key, int defaultValue = 0)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        // Handle different types from MCP deserialization
        if (value is int i)
        {
            return i;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
        {
            if (jsonElement.TryGetInt32(out var intVal))
            {
                return intVal;
            }

            if (jsonElement.TryGetDouble(out var dblVal))
            {
                if (dblVal >= int.MinValue && dblVal <= int.MaxValue)
                {
                    return (int)dblVal;
                }

                return defaultValue;
            }

            return defaultValue;
        }

        if (value is long l)
        {
            return (int)l;
        }

        return defaultValue;
    }

    /// <summary>
    /// Extract boolean parameter with default value
    /// </summary>
    protected static bool GetBoolParam(IDictionary<string, object?> parameters, string key, bool defaultValue = false)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        // Handle different types from MCP deserialization
        if (value is bool b)
        {
            return b;
        }

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (jsonElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Extract string parameter with default value
    /// </summary>
    protected static string GetStringParam(IDictionary<string, object?> parameters, string key, string defaultValue = "")
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        // Handle different types from MCP deserialization
        if (value is string s)
        {
            return s;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            return jsonElement.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    /// <summary>
    /// Extract string parameter that may be null (no default value)
    /// </summary>
    protected static string? GetStringParamNullable(IDictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        // Handle different types from MCP deserialization
        if (value is string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            var str = jsonElement.GetString();
            return string.IsNullOrEmpty(str) ? null : str;
        }

        return null;
    }

    #endregion

    #region File Output Helpers

    /// <summary>
    /// Validates that a file path is safe for write operations (no path traversal, UNC, or device paths)
    /// </summary>
    protected static bool IsPathSafe(string path, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "Path is empty or whitespace";
                return false;
            }

            var fullPath = Path.GetFullPath(path);

            // Reject UNC paths
            if (fullPath.StartsWith(@"\\"))
            {
                errorMessage = "UNC paths are not allowed";
                return false;
            }

            // Reject device paths
            if (fullPath.StartsWith(@"\\.\") || fullPath.StartsWith(@"\\?\"))
            {
                errorMessage = "Device paths are not allowed";
                return false;
            }

            // Ensure parent directory exists
            var dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir))
            {
                errorMessage = "Invalid path - no parent directory";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid path: {ex.Message}";
            return false;
        }
    }

    protected bool TryWriteJsonToFile<T>(
        string outputPath,
        T value,
        JsonSerializerOptions options,
        out long fileSizeBytes,
        out string? errorMessage)
    {
        fileSizeBytes = 0;
        errorMessage = null;

        if (!IsPathSafe(outputPath, out errorMessage))
        {
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, options);
            File.WriteAllText(outputPath, json);
            fileSizeBytes = new FileInfo(outputPath).Length;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    #endregion

    #region Unit Conversion Helpers

    /// <summary>
    /// Convert millimeters to meters (SolidWorks uses meters internally)
    /// </summary>
    protected static double MmToMeters(double mm) => mm / 1000.0;

    /// <summary>
    /// Convert meters to millimeters (MCP tools use millimeters for user-facing values)
    /// </summary>
    protected static double MetersToMm(double meters) => meters * 1000.0;

    /// <summary>
    /// Convert degrees to radians (SolidWorks uses radians for angles)
    /// </summary>
    protected static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);

    /// <summary>
    /// Convert radians to degrees (for user-facing values)
    /// </summary>
    protected static double RadiansToDegrees(double radians) => radians * (180.0 / Math.PI);

    #endregion
}
