using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FurniOx.SolidWorks.Core.Tests;

internal sealed class TestableOperationHandler : OperationHandlerBase
{
    public TestableOperationHandler()
        : base(
            new SolidWorksConnection(
                NullLogger<SolidWorksConnection>.Instance,
                new SolidWorksSettings()),
            new SolidWorksSettings(),
            NullLogger.Instance)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    public static new double GetDoubleParam(IDictionary<string, object?> parameters, string key, double defaultValue = 0.0)
        => OperationHandlerBase.GetDoubleParam(parameters, key, defaultValue);

    public static new int GetIntParam(IDictionary<string, object?> parameters, string key, int defaultValue = 0)
        => OperationHandlerBase.GetIntParam(parameters, key, defaultValue);

    public static new bool GetBoolParam(IDictionary<string, object?> parameters, string key, bool defaultValue = false)
        => OperationHandlerBase.GetBoolParam(parameters, key, defaultValue);

    public static new string GetStringParam(IDictionary<string, object?> parameters, string key, string defaultValue = "")
        => OperationHandlerBase.GetStringParam(parameters, key, defaultValue);

    public static new string? GetStringParamNullable(IDictionary<string, object?> parameters, string key)
        => OperationHandlerBase.GetStringParamNullable(parameters, key);

    public static new double MmToMeters(double millimeters) => OperationHandlerBase.MmToMeters(millimeters);
    public static new double MetersToMm(double meters) => OperationHandlerBase.MetersToMm(meters);
    public static new double DegreesToRadians(double degrees) => OperationHandlerBase.DegreesToRadians(degrees);
    public static new double RadiansToDegrees(double radians) => OperationHandlerBase.RadiansToDegrees(radians);
    public static new bool IsPathSafe(string path, out string? error) => OperationHandlerBase.IsPathSafe(path, out error);
}

internal static class OperationHandlerJson
{
    public static JsonElement Number(string literal) => JsonDocument.Parse(literal).RootElement;
    public static JsonElement Bool(bool value) => JsonDocument.Parse(value ? "true" : "false").RootElement;
    public static JsonElement String(string value) => JsonDocument.Parse($"\"{value}\"").RootElement;
}
