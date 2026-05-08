using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

internal sealed class CustomPropertyReadOperations : OperationHandlerBase
{
    public CustomPropertyReadOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<CustomPropertyReadOperations> logger)
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
            "CustomProperty.Get" => GetCustomPropertyAsync(parameters),
            "CustomProperty.GetAll" => GetAllCustomPropertiesAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown read custom property operation: {operation}"))
        };
    }

    private Task<ExecutionResult> GetCustomPropertyAsync(IDictionary<string, object?> parameters)
    {
        var (scope, failure) = TargetDocumentResolutionSupport.TryCreateScope(_connection, parameters);
        if (scope == null)
        {
            return Task.FromResult(ExecutionResult.Failure(failure ?? "No active document or selected component"));
        }

        using (scope)
        {
            if (!CustomPropertySupport.TryGetRequiredString(parameters, CustomPropertySupport.ParamName, allowEmpty: false, out var propertyName, out var parameterFailure))
            {
                return Task.FromResult(parameterFailure);
            }

            var configName = GetStringParam(parameters, CustomPropertySupport.ParamConfiguration);
            var propertyManager = CustomPropertySupport.TryGetPropertyManager(scope.TargetModel, configName);
            if (propertyManager == null)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to get custom property manager for configuration: {configName}"));
            }

            string valueOut = string.Empty;
            string resolvedValueOut = string.Empty;
            var wasResolved = false;
            var linkToProperty = false;
            var result = propertyManager.Get6(
                propertyName,
                false,
                out valueOut,
                out resolvedValueOut,
                out wasResolved,
                out linkToProperty);

            if (result == 1)
            {
                return Task.FromResult(ExecutionResult.Failure($"Property '{propertyName}' not found"));
            }

            var typeId = propertyManager.GetType2(propertyName);

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Name = propertyName,
                Value = valueOut,
                ResolvedValue = resolvedValueOut,
                WasResolved = wasResolved,
                LinkToProperty = linkToProperty,
                Type = CustomPropertySupport.GetTypeName(typeId),
                Configuration = CustomPropertySupport.FormatConfigurationLabel(configName),
                ComponentName = scope.ComponentName,
                ComponentPath = scope.ComponentPath
            }));
        }
    }

    private Task<ExecutionResult> GetAllCustomPropertiesAsync(IDictionary<string, object?> parameters)
    {
        var (scope, failure) = TargetDocumentResolutionSupport.TryCreateScope(_connection, parameters);
        if (scope == null)
        {
            return Task.FromResult(ExecutionResult.Failure(failure ?? "No active document or selected component"));
        }

        using (scope)
        {
            var configName = GetStringParam(parameters, CustomPropertySupport.ParamConfiguration);
            var propertyManager = CustomPropertySupport.TryGetPropertyManager(scope.TargetModel, configName);
            if (propertyManager == null)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to get custom property manager for configuration: {configName}"));
            }

            var propertyCount = propertyManager.Count;
            var names = propertyManager.GetNames().ToStringArraySafe();
            if (names.Length == 0)
            {
                return Task.FromResult(ExecutionResult.SuccessResult(new
                {
                    Count = 0,
                    Properties = Array.Empty<object>(),
                    Configuration = CustomPropertySupport.FormatConfigurationLabel(configName),
                    PropertyManagerCount = propertyCount,
                    ComponentName = scope.ComponentName,
                    ComponentPath = scope.ComponentPath,
                    Note = propertyCount > 0
                        ? "PropertyManager reports properties exist but GetNames() returned empty - possible COM marshaling issue"
                        : "No custom properties defined"
                }));
            }

            var properties = new List<object>();
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                string valueOut = string.Empty;
                string resolvedValueOut = string.Empty;
                var wasResolved = false;
                var linkToProperty = false;
                var result = propertyManager.Get6(
                    name,
                    false,
                    out valueOut,
                    out resolvedValueOut,
                    out wasResolved,
                    out linkToProperty);

                if (result == 1)
                {
                    continue;
                }

                var typeId = propertyManager.GetType2(name);
                properties.Add(new
                {
                    Name = name,
                    Value = valueOut,
                    ResolvedValue = resolvedValueOut,
                    WasResolved = wasResolved,
                    LinkToProperty = linkToProperty,
                    Type = CustomPropertySupport.GetTypeName(typeId)
                });
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Count = properties.Count,
                Properties = properties,
                Configuration = CustomPropertySupport.FormatConfigurationLabel(configName),
                ComponentName = scope.ComponentName,
                ComponentPath = scope.ComponentPath
            }));
        }
    }
}
