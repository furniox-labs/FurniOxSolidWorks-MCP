using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Shared.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

internal static class CustomPropertySupport
{
    internal const string ParamName = "Name";
    internal const string ParamValue = "Value";
    internal const string ParamConfiguration = "Configuration";
    internal const string ParamType = "Type";

    private static readonly Dictionary<string, swCustomInfoType_e> TypeNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = swCustomInfoType_e.swCustomInfoText,
        ["number"] = swCustomInfoType_e.swCustomInfoNumber,
        ["double"] = swCustomInfoType_e.swCustomInfoDouble,
        ["yesorno"] = swCustomInfoType_e.swCustomInfoYesOrNo,
        ["date"] = swCustomInfoType_e.swCustomInfoDate
    };

    public static string GetTypeName(int typeId) => typeId switch
    {
        (int)swCustomInfoType_e.swCustomInfoText => "Text",
        (int)swCustomInfoType_e.swCustomInfoNumber => "Number",
        (int)swCustomInfoType_e.swCustomInfoYesOrNo => "YesOrNo",
        (int)swCustomInfoType_e.swCustomInfoDate => "Date",
        (int)swCustomInfoType_e.swCustomInfoDouble => "Double",
        _ => "Unknown"
    };

    public static string FormatConfigurationLabel(string configName)
        => string.IsNullOrEmpty(configName) ? "File-level" : configName;

    public static ICustomPropertyManager? TryGetPropertyManager(ModelDoc2 targetModel, string configName)
        => (ICustomPropertyManager?)targetModel.Extension.CustomPropertyManager[configName];

    public static bool TryGetRequiredString(
        IDictionary<string, object?> parameters,
        string key,
        bool allowEmpty,
        out string value,
        out ExecutionResult failure)
    {
        if (parameters.TryGetValue(key, out var obj) && obj is string stringValue)
        {
            if (allowEmpty || !string.IsNullOrWhiteSpace(stringValue))
            {
                value = stringValue;
                failure = default!;
                return true;
            }
        }

        value = string.Empty;
        failure = ExecutionResult.Failure($"Missing or invalid '{key}' parameter");
        return false;
    }

    public static swCustomInfoType_e ResolveRequestedType(string? typeName, string propertyValue)
    {
        if (!TypeNameMap.TryGetValue(typeName ?? "text", out var propertyType))
        {
            propertyType = swCustomInfoType_e.swCustomInfoText;
        }

        if (propertyType == swCustomInfoType_e.swCustomInfoNumber
            && (propertyValue.Contains('.') || propertyValue.Contains(',')))
        {
            return swCustomInfoType_e.swCustomInfoDouble;
        }

        return propertyType;
    }
}
