namespace FurniOx.SolidWorks.Shared.Models;

public sealed class ExecutionResult
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public string? Message { get; init; }

    public static ExecutionResult SuccessResult(object? data = null, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ExecutionResult Failure(string message, object? data = null) =>
        new() { Success = false, Message = message, Data = data };
}
