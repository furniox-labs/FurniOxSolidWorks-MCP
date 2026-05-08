using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Read/write SolidWorks' reference search folders (file locations SW probes
/// when resolving missing references in assemblies). Wraps
/// ISldWorks.GetSearchFolders / SetSearchFolders with semicolon-joined strings.
/// FolderType is fixed to swDocumentType (0) — the only enum value defined in
/// swSearchFolderTypes_e.
/// </summary>
public sealed class DocumentReferenceSearchPathOperations : OperationHandlerBase
{
    private static readonly int FolderType = (int)swSearchFolderTypes_e.swDocumentType;

    public DocumentReferenceSearchPathOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentReferenceSearchPathOperations> logger)
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
            DocumentReferenceSearchPathOperationNames.GetReferenceSearchPath => GetAsync(),
            DocumentReferenceSearchPathOperationNames.SetReferenceSearchPath => SetAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown reference search path operation: {operation}"))
        };
    }

    private Task<ExecutionResult> GetAsync()
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var raw = SafeGetSearchFolders(app);
        return Task.FromResult(ExecutionResult.SuccessResult(BuildResult(raw)));
    }

    private Task<ExecutionResult> SetAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        if (!TryParsePathsParam(parameters, out var paths, out var parseError))
        {
            return Task.FromResult(ExecutionResult.Failure(parseError!));
        }

        var append = GetBoolParam(parameters, "Append", false);
        var previousRaw = SafeGetSearchFolders(app);

        var sanitized = SanitizePaths(paths);
        if (sanitized.Count == 0 && !append)
        {
            // Allow explicit clear: SetSearchFolders("") wipes the list. Keep that explicit.
            var cleared = TryApply(app, string.Empty, out var clearError);
            if (!cleared)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    $"SolidWorks rejected SetSearchFolders(''): {clearError}",
                    new { Previous = previousRaw }));
            }
            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Cleared = true,
                Previous = BuildResult(previousRaw),
                Applied = BuildResult(string.Empty)
            }));
        }

        var combined = append
            ? CombineWithExisting(previousRaw, sanitized)
            : sanitized;
        var joined = string.Join(";", combined);

        var ok = TryApply(app, joined, out var applyError);
        if (!ok)
        {
            return Task.FromResult(ExecutionResult.Failure(
                $"SolidWorks rejected SetSearchFolders: {applyError}",
                new
                {
                    Previous = BuildResult(previousRaw),
                    AttemptedFolders = combined,
                    AttemptedRaw = joined
                }));
        }

        var newRaw = SafeGetSearchFolders(app);
        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Applied = BuildResult(newRaw),
            Previous = BuildResult(previousRaw),
            Append = append
        }));
    }

    private static string SafeGetSearchFolders(SldWorks app)
    {
        try
        {
            return app.GetSearchFolders(FolderType) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool TryApply(SldWorks app, string joined, out string? error)
    {
        error = null;
        try
        {
            var ok = app.SetSearchFolders(FolderType, joined);
            if (!ok)
            {
                error = "SetSearchFolders returned false";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetSearchFolders threw");
            error = ex.Message;
            return false;
        }
    }

    private static object BuildResult(string raw) => new
    {
        Folders = SplitFolders(raw),
        Raw = raw
    };

    private static string[] SplitFolders(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();
    }

    private static List<string> SanitizePaths(IEnumerable<string> paths)
    {
        // Drop empties, trim, collapse case-insensitive duplicates, preserve order.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            var trimmed = p.Trim();
            // Reject embedded semicolons — the API uses ';' as separator and SW silently
            // splits them into separate entries, which is rarely what the caller intended.
            if (trimmed.Contains(';'))
            {
                throw new ArgumentException(
                    $"Path '{trimmed}' contains a ';' separator. Pass each folder as a separate array element.");
            }
            if (seen.Add(trimmed)) result.Add(trimmed);
        }
        return result;
    }

    private static List<string> CombineWithExisting(string existingRaw, List<string> additions)
    {
        var existing = SplitFolders(existingRaw);
        var combined = new List<string>(existing);
        var seen = new HashSet<string>(combined, StringComparer.OrdinalIgnoreCase);
        foreach (var p in additions)
        {
            if (seen.Add(p)) combined.Add(p);
        }
        return combined;
    }

    private static bool TryParsePathsParam(
        IDictionary<string, object?> parameters,
        out List<string> paths,
        out string? error)
    {
        paths = new List<string>();
        error = null;

        if (!parameters.TryGetValue("Paths", out var raw) || raw == null)
        {
            error = "Missing 'Paths' parameter (array of folder paths).";
            return false;
        }

        switch (raw)
        {
            case string single when !string.IsNullOrWhiteSpace(single):
                // Tolerate caller-passed semicolon-joined string for convenience.
                paths.AddRange(single.Split(';', StringSplitOptions.RemoveEmptyEntries));
                return true;
            case IEnumerable<string> typed:
                paths.AddRange(typed);
                return true;
            case JsonElement el when el.ValueKind == JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) paths.Add(s);
                    }
                }
                return true;
            case JsonElement el when el.ValueKind == JsonValueKind.String:
                var sval = el.GetString();
                if (!string.IsNullOrWhiteSpace(sval))
                {
                    paths.AddRange(sval.Split(';', StringSplitOptions.RemoveEmptyEntries));
                    return true;
                }
                error = "'Paths' parameter is empty.";
                return false;
            case System.Collections.IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    if (item is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        paths.Add(s);
                    }
                    else if (item is JsonElement je && je.ValueKind == JsonValueKind.String)
                    {
                        var s2 = je.GetString();
                        if (!string.IsNullOrWhiteSpace(s2)) paths.Add(s2);
                    }
                }
                return true;
            default:
                error = $"'Paths' must be an array of strings (got {raw.GetType().Name}).";
                return false;
        }
    }
}
