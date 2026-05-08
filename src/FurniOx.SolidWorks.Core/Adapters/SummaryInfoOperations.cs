using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Analysis;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Handles document summary information operations (Author, Title, Subject, etc.)
/// These are OLE built-in properties, separate from custom properties.
/// </summary>
public sealed class SummaryInfoOperations : OperationHandlerBase
{
    internal static readonly Dictionary<string, swSummInfoField_e> WritableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = swSummInfoField_e.swSumInfoTitle,
        ["subject"] = swSummInfoField_e.swSumInfoSubject,
        ["author"] = swSummInfoField_e.swSumInfoAuthor,
        ["keywords"] = swSummInfoField_e.swSumInfoKeywords,
        ["comments"] = swSummInfoField_e.swSumInfoComment
    };

    internal static readonly Dictionary<string, swSummInfoField_e> ReadOnlyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["savedby"] = swSummInfoField_e.swSumInfoSavedBy,
        ["createdate"] = swSummInfoField_e.swSumInfoCreateDate2,
        ["savedate"] = swSummInfoField_e.swSumInfoSaveDate2
    };

    public SummaryInfoOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SummaryInfoOperations> logger)
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
            "SummaryInfo.GetAll" => GetAllSummaryInfoAsync(parameters),
            "SummaryInfo.Get" => GetSummaryInfoAsync(parameters),
            "SummaryInfo.Set" => SetSummaryInfoAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown summary info operation: {operation}"))
        };
    }

    internal static bool TryResolveField(string fieldName, out swSummInfoField_e fieldEnum)
    {
        if (WritableFields.TryGetValue(fieldName, out fieldEnum))
        {
            return true;
        }

        if (ReadOnlyFields.TryGetValue(fieldName, out fieldEnum))
        {
            return true;
        }

        fieldEnum = default;
        return false;
    }

    private Task<ExecutionResult> GetAllSummaryInfoAsync(IDictionary<string, object?> parameters)
    {
        var (scope, failure) = TargetDocumentResolutionSupport.TryCreateScope(_connection, parameters);
        if (scope == null)
        {
            return Task.FromResult(ExecutionResult.Failure(failure ?? "No target document"));
        }

        using (scope)
        {
            var info = AnalysisExtractionSupport.ExtractSummaryInfo(scope.TargetModel, _logger);
            if (info == null)
            {
                return Task.FromResult(ExecutionResult.Failure("Failed to extract summary information"));
            }

            if (scope.ComponentName != null)
            {
                return Task.FromResult(ExecutionResult.SuccessResult(new
                {
                    info.Title,
                    info.Subject,
                    info.Author,
                    info.Keywords,
                    info.Comments,
                    info.SavedBy,
                    info.CreateDate,
                    info.SaveDate,
                    ComponentName = scope.ComponentName,
                    ComponentPath = scope.ComponentPath
                }));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(info));
        }
    }

    private Task<ExecutionResult> GetSummaryInfoAsync(IDictionary<string, object?> parameters)
    {
        var (scope, failure) = TargetDocumentResolutionSupport.TryCreateScope(_connection, parameters);
        if (scope == null)
        {
            return Task.FromResult(ExecutionResult.Failure(failure ?? "No target document"));
        }

        using (scope)
        {
            try
            {
                var fieldName = GetStringParam(parameters, "Field");
                if (string.IsNullOrEmpty(fieldName))
                {
                    return Task.FromResult(ExecutionResult.Failure("Missing 'Field' parameter. Valid fields: title, subject, author, keywords, comments, savedby, createdate, savedate"));
                }

                if (!TryResolveField(fieldName, out var fieldEnum))
                {
                    return Task.FromResult(ExecutionResult.Failure($"Unknown field '{fieldName}'. Valid fields: title, subject, author, keywords, comments, savedby, createdate, savedate"));
                }

                var result = new Dictionary<string, object?>
                {
                    ["field"] = fieldName,
                    ["value"] = scope.TargetModel.SummaryInfo[(int)fieldEnum] ?? string.Empty
                };

                if (scope.ComponentName != null)
                {
                    result["componentName"] = scope.ComponentName;
                    result["componentPath"] = scope.ComponentPath;
                }

                return Task.FromResult(ExecutionResult.SuccessResult(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to read summary field '{GetStringParam(parameters, "Field")}': {ex.Message}"));
            }
        }
    }

    private Task<ExecutionResult> SetSummaryInfoAsync(IDictionary<string, object?> parameters)
    {
        var (scope, failure) = TargetDocumentResolutionSupport.TryCreateScope(_connection, parameters);
        if (scope == null)
        {
            return Task.FromResult(ExecutionResult.Failure(failure ?? "No target document"));
        }

        using (scope)
        {
            try
            {
                var fieldName = GetStringParam(parameters, "Field");
                if (string.IsNullOrEmpty(fieldName))
                {
                    return Task.FromResult(ExecutionResult.Failure("Missing 'Field' parameter. Writable fields: title, subject, author, keywords, comments"));
                }

                if (!WritableFields.TryGetValue(fieldName, out var fieldEnum))
                {
                    if (ReadOnlyFields.ContainsKey(fieldName))
                    {
                        return Task.FromResult(ExecutionResult.Failure($"Field '{fieldName}' is read-only (system-managed)"));
                    }

                    return Task.FromResult(ExecutionResult.Failure($"Unknown field '{fieldName}'. Writable fields: title, subject, author, keywords, comments"));
                }

                var value = GetStringParam(parameters, "Value");
                scope.TargetModel.SummaryInfo[(int)fieldEnum] = value;

                var result = new Dictionary<string, object?>
                {
                    ["set"] = true,
                    ["field"] = fieldName,
                    ["value"] = value
                };

                if (scope.ComponentName != null)
                {
                    result["componentName"] = scope.ComponentName;
                    result["componentPath"] = scope.ComponentPath;
                }

                return Task.FromResult(ExecutionResult.SuccessResult(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to set summary field: {ex.Message}"));
            }
        }
    }
}
