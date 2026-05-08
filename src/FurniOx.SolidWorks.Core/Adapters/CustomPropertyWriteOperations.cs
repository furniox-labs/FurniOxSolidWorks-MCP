using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

internal sealed class CustomPropertyWriteOperations : OperationHandlerBase
{
    public CustomPropertyWriteOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<CustomPropertyWriteOperations> logger)
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
            "CustomProperty.Set" => SetCustomPropertyAsync(parameters),
            "CustomProperty.Delete" => DeleteCustomPropertyAsync(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown write custom property operation: {operation}"))
        };
    }

    private Task<ExecutionResult> SetCustomPropertyAsync(IDictionary<string, object?> parameters)
    {
        var (scope, failure) = TargetDocumentResolutionSupport.TryCreateScope(_connection, parameters);
        if (scope == null)
        {
            return Task.FromResult(ExecutionResult.Failure(failure ?? "No active document or selected component"));
        }

        using (scope)
        {
            if (!CustomPropertySupport.TryGetRequiredString(parameters, CustomPropertySupport.ParamName, allowEmpty: false, out var propertyName, out var nameFailure))
            {
                return Task.FromResult(nameFailure);
            }

            if (!CustomPropertySupport.TryGetRequiredString(parameters, CustomPropertySupport.ParamValue, allowEmpty: true, out var propertyValue, out var valueFailure))
            {
                return Task.FromResult(valueFailure);
            }

            var configName = GetStringParam(parameters, CustomPropertySupport.ParamConfiguration);
            var propertyManager = CustomPropertySupport.TryGetPropertyManager(scope.TargetModel, configName);
            if (propertyManager == null)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to get custom property manager for configuration: {configName}"));
            }

            var typeParam = GetStringParam(parameters, CustomPropertySupport.ParamType, "text");
            var propertyType = CustomPropertySupport.ResolveRequestedType(typeParam, propertyValue);

            var result = propertyManager.Add3(
                propertyName,
                (int)propertyType,
                propertyValue,
                (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

            if (result != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged
                && propertyType != swCustomInfoType_e.swCustomInfoText)
            {
                _logger.LogWarning(
                    "Add3 failed with type {Type} (code {Code}) for '{Name}', retrying with Add3('0')+Set2 to preserve type",
                    propertyType,
                    result,
                    propertyName);

                result = propertyManager.Add3(
                    propertyName,
                    (int)propertyType,
                    "0",
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

                if (result == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
                {
                    propertyManager.Set2(propertyName, propertyValue);
                }
            }

            if (result != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
            {
                var errorMessage = result switch
                {
                    1 => "Generic failure - check property name/value",
                    2 => "Type mismatch with existing property - try deleting first",
                    _ => $"Unknown error code: {result}"
                };
                return Task.FromResult(ExecutionResult.Failure($"Failed to set property '{propertyName}': {errorMessage}"));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Set = true,
                Name = propertyName,
                Value = propertyValue,
                Configuration = CustomPropertySupport.FormatConfigurationLabel(configName),
                ComponentName = scope.ComponentName,
                ComponentPath = scope.ComponentPath
            }));
        }
    }

    private Task<ExecutionResult> DeleteCustomPropertyAsync(IDictionary<string, object?> parameters)
    {
        var (scope, failure) = TargetDocumentResolutionSupport.TryCreateScope(_connection, parameters);
        if (scope == null)
        {
            return Task.FromResult(ExecutionResult.Failure(failure ?? "No active document or selected component"));
        }

        using (scope)
        {
            if (!CustomPropertySupport.TryGetRequiredString(parameters, CustomPropertySupport.ParamName, allowEmpty: false, out var propertyName, out var nameFailure))
            {
                return Task.FromResult(nameFailure);
            }

            var configName = GetStringParam(parameters, CustomPropertySupport.ParamConfiguration);
            var propertyManager = CustomPropertySupport.TryGetPropertyManager(scope.TargetModel, configName);
            if (propertyManager == null)
            {
                return Task.FromResult(ExecutionResult.Failure($"Failed to get custom property manager for configuration: {configName}"));
            }

            var result = propertyManager.Delete2(propertyName);
            if (result != 0)
            {
                var errorMessage = result switch
                {
                    1 => "Property does not exist",
                    2 => "Cannot delete linked property",
                    _ => $"Unknown error code: {result}"
                };
                return Task.FromResult(ExecutionResult.Failure($"Failed to delete property '{propertyName}': {errorMessage}"));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Deleted = true,
                Name = propertyName,
                Configuration = CustomPropertySupport.FormatConfigurationLabel(configName),
                ComponentName = scope.ComponentName,
                ComponentPath = scope.ComponentPath
            }));
        }
    }
}
