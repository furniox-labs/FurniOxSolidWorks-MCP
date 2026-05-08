using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Shared helpers for single-component and batch suppression handlers.
/// </summary>
internal static class DocumentSuppressionShared
{
    internal static bool TryResolveAssemblyComponent(
        SolidWorksConnection connection,
        IDictionary<string, object?> parameters,
        out IComponent2? component,
        out ExecutionResult? failure)
    {
        component = null;
        failure = null;

        var app = connection.Application;
        if (app == null) { failure = ExecutionResult.Failure("Not connected to SolidWorks"); return false; }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) { failure = ExecutionResult.Failure("No active document"); return false; }
        if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            failure = ExecutionResult.Failure("Active document must be an assembly");
            return false;
        }

        var componentName = GetStringParam(parameters, "ComponentName");
        if (string.IsNullOrWhiteSpace(componentName))
        {
            failure = ExecutionResult.Failure("Missing 'ComponentName' parameter (use Name from analyze_assembly)");
            return false;
        }

        var assembly = (IAssemblyDoc)model;
        component = (IComponent2?)assembly.GetComponentByName(componentName);
        if (component == null)
        {
            failure = ExecutionResult.Failure($"Component '{componentName}' not found in assembly");
            return false;
        }
        return true;
    }

    internal static bool TryRequireActiveAssembly(
        SolidWorksConnection connection,
        out IAssemblyDoc? assembly,
        out ExecutionResult? failure)
    {
        assembly = null;
        failure = null;

        var app = connection.Application;
        if (app == null) { failure = ExecutionResult.Failure("Not connected to SolidWorks"); return false; }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) { failure = ExecutionResult.Failure("No active document"); return false; }
        if (((IModelDoc2)model).GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            failure = ExecutionResult.Failure("Active document must be an assembly");
            return false;
        }

        assembly = (IAssemblyDoc)model;
        return true;
    }

    internal static List<string>? ReadComponentNamesParam(IDictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("ComponentNames", out var value) || value == null)
        {
            return null;
        }

        if (value is string[] names)
        {
            return names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
        }

        if (value is IEnumerable<string> enumerable)
        {
            return enumerable.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
        }

        if (value is string text)
        {
            return SplitComponentNames(text);
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(ReadComponentName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!.Trim())
                    .ToList(),
                JsonValueKind.String => SplitComponentNames(element.GetString() ?? string.Empty),
                _ => null
            };
        }

        return null;
    }

    internal static List<string> SplitComponentNames(string text)
    {
        return text
            .Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    internal static List<string> ParseComponentNameItems(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Input must be a JSON array of component names or {ComponentName} objects.");
        }

        return doc.RootElement.EnumerateArray()
            .Select(ReadComponentName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .ToList();
    }

    internal static string? ReadComponentName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return ReadString(element, "ComponentName", "componentName", "Name", "name");
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        return null;
    }

    internal static (bool Ok, int AfterState, string? Error) ApplySuppression(IComponent2 component, int targetState)
    {
        int returnCode;
        try
        {
            returnCode = component.SetSuppression2(targetState);
        }
        catch (Exception ex)
        {
            return (false, component.ReadSuppressionState(), $"SetSuppression2 threw: {ex.Message}");
        }

        var actual = component.ReadSuppressionState();
        if (returnCode == (int)swComponentSuppressionState_e.swComponentInternalIdMismatch)
        {
            return (false, actual, "Internal ID mismatch (component identity changed)");
        }
        // SetSuppression2 may return a stronger state than requested, e.g.
        // FullyResolved after asking for Resolved. Treat those supersets as success.
        if (StateSatisfiesRequest(targetState, actual)) return (true, actual, null);
        return (false, actual, $"SW returned state {StateName(actual)} ({actual}); expected {StateName(targetState)} ({targetState})");
    }

    internal static bool StateSatisfiesRequest(int requested, int actual)
    {
        if (requested == (int)swComponentSuppressionState_e.swComponentResolved)
        {
            return actual == (int)swComponentSuppressionState_e.swComponentResolved
                || actual == (int)swComponentSuppressionState_e.swComponentFullyResolved;
        }

        if (requested == (int)swComponentSuppressionState_e.swComponentLightweight)
        {
            return actual == (int)swComponentSuppressionState_e.swComponentLightweight
                || actual == (int)swComponentSuppressionState_e.swComponentFullyLightweight;
        }

        return actual == requested;
    }

    internal static object BuildComponentStateInfo(IComponent2 component)
    {
        var state = component.ReadSuppressionState();
        return new
        {
            ComponentName = component.Name2 ?? string.Empty,
            Path = component.GetPathName() ?? string.Empty,
            State = StateName(state),
            StateCode = state,
            IsSuppressed = state == (int)swComponentSuppressionState_e.swComponentSuppressed,
            IsLightweight = state == (int)swComponentSuppressionState_e.swComponentLightweight
                || state == (int)swComponentSuppressionState_e.swComponentFullyLightweight,
            IsResolved = state == (int)swComponentSuppressionState_e.swComponentResolved
                || state == (int)swComponentSuppressionState_e.swComponentFullyResolved
        };
    }

    internal static bool TryParseStateParam(
        IDictionary<string, object?> parameters,
        string key,
        out int state,
        out string? error)
    {
        state = 0;
        error = null;
        var raw = GetStringParam(parameters, key);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return TryParseStateString(raw, out state, out error);
        }

        // Allow numeric state codes for callers that already know the enum.
        if (parameters.TryGetValue(key, out var value) && value is JsonElement el && el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetInt32(out var i)) { state = i; return ValidateStateCode(state, out error); }
        }

        error = $"Missing '{key}' parameter. Use one of: Suppressed, Lightweight, Resolved, FullyResolved, FullyLightweight.";
        return false;
    }

    internal static bool TryParseStateString(string? raw, out int state, out string? error)
    {
        state = 0;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "State value is empty.";
            return false;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "suppressed":
                state = (int)swComponentSuppressionState_e.swComponentSuppressed;
                return true;
            case "lightweight":
                state = (int)swComponentSuppressionState_e.swComponentLightweight;
                return true;
            case "fullyresolved":
            case "fully_resolved":
                state = (int)swComponentSuppressionState_e.swComponentFullyResolved;
                return true;
            case "resolved":
                state = (int)swComponentSuppressionState_e.swComponentResolved;
                return true;
            case "fullylightweight":
            case "fully_lightweight":
                state = (int)swComponentSuppressionState_e.swComponentFullyLightweight;
                return true;
            default:
                if (int.TryParse(raw, out var i) && ValidateStateCode(i, out _))
                {
                    state = i;
                    return true;
                }
                error = $"Unknown state '{raw}'. Use one of: Suppressed, Lightweight, Resolved, FullyResolved, FullyLightweight.";
                return false;
        }
    }

    private static bool ValidateStateCode(int code, out string? error)
    {
        error = null;
        if (code >= 0 && code <= 4) return true;
        error = $"Invalid state code {code}. Valid codes: 0=Suppressed, 1=Lightweight, 2=FullyResolved, 3=Resolved, 4=FullyLightweight.";
        return false;
    }

    internal static string StateName(int code) => code switch
    {
        0 => "Suppressed",
        1 => "Lightweight",
        2 => "FullyResolved",
        3 => "Resolved",
        4 => "FullyLightweight",
        5 => "InternalIdMismatch",
        _ => $"Unknown({code})"
    };

    private static string GetStringParam(IDictionary<string, object?> parameters, string key, string defaultValue = "")
    {
        if (!parameters.TryGetValue(key, out var value) || value == null) return defaultValue;
        if (value is string s) return s;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.String) return je.GetString() ?? defaultValue;
        return defaultValue;
    }
}
