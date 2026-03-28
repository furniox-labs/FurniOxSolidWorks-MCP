using FurniOx.SolidWorks.Shared.Models;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Unit tests for <see cref="ExecutionResult"/> covering the factory methods
/// SuccessResult and Failure, and verifying property semantics.
/// </summary>
public class ExecutionResultTests
{
    // ========== SuccessResult ==========

    [Fact]
    public void SuccessResult_HasSuccessTrue()
    {
        var result = ExecutionResult.SuccessResult();

        Assert.True(result.Success);
    }

    [Fact]
    public void SuccessResult_WithData_StoresData()
    {
        var payload = new { Value = 42, Label = "test" };

        var result = ExecutionResult.SuccessResult(payload);

        Assert.Equal(payload, result.Data);
    }

    [Fact]
    public void SuccessResult_WithMessage_StoresMessage()
    {
        const string message = "Operation completed successfully.";

        var result = ExecutionResult.SuccessResult(message: message);

        Assert.Equal(message, result.Message);
    }

    [Fact]
    public void SuccessResult_WithoutData_HasNullData()
    {
        var result = ExecutionResult.SuccessResult();

        Assert.Null(result.Data);
    }

    [Fact]
    public void SuccessResult_WithoutMessage_HasNullMessage()
    {
        var result = ExecutionResult.SuccessResult();

        Assert.Null(result.Message);
    }

    [Fact]
    public void SuccessResult_WithBothDataAndMessage_StoresBoth()
    {
        var payload = new { Id = 1 };
        const string message = "Sketch created.";

        var result = ExecutionResult.SuccessResult(payload, message);

        Assert.True(result.Success);
        Assert.Equal(payload, result.Data);
        Assert.Equal(message, result.Message);
    }

    // ========== Failure ==========

    [Fact]
    public void Failure_HasSuccessFalse()
    {
        var result = ExecutionResult.Failure("Something went wrong.");

        Assert.False(result.Success);
    }

    [Fact]
    public void Failure_StoresMessage()
    {
        const string message = "COM object disconnected.";

        var result = ExecutionResult.Failure(message);

        Assert.Equal(message, result.Message);
    }

    [Fact]
    public void Failure_WithData_StoresData()
    {
        var diagnostics = new { ErrorCode = 0x80004005, Source = "SolidWorks" };

        var result = ExecutionResult.Failure("COM error.", diagnostics);

        Assert.Equal(diagnostics, result.Data);
    }

    [Fact]
    public void Failure_WithoutData_HasNullData()
    {
        var result = ExecutionResult.Failure("Operation failed.");

        Assert.Null(result.Data);
    }

    // ========== Symmetry / contract ==========

    [Fact]
    public void SuccessResult_AndFailure_AreOpposite_OnSuccessFlag()
    {
        var success = ExecutionResult.SuccessResult();
        var failure = ExecutionResult.Failure("Error.");

        Assert.NotEqual(success.Success, failure.Success);
    }

    [Fact]
    public void Failure_MessageIsRequired_AndNeverNull()
    {
        // The Failure factory mandates a message parameter.
        // Passing an empty string is legal but the property must not be null.
        var result = ExecutionResult.Failure(string.Empty);

        Assert.NotNull(result.Message);
    }
}
