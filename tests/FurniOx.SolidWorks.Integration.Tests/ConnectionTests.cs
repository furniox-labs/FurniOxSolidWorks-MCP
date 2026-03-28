using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.SmartRouting;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;

namespace FurniOx.SolidWorks.Integration.Tests;

/// <summary>
/// Tests SolidWorks COM connection
/// NOTE: These tests require SolidWorks to be installed
/// </summary>
public class ConnectionTests
{
    private readonly ITestOutputHelper _output;

    public ConnectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CanConnect_ToSolidWorks()
    {
        if (!IntegrationTestGate.IsEnabled(_output))
        {
            return;
        }

        // Arrange
        var settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = 3,
                ResetTimeoutSeconds = 30
            }
        };

        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var circuitBreaker = new CircuitBreaker(
            settings,
            loggerFactory.CreateLogger<CircuitBreaker>());

        var connection = new SolidWorksConnection(
            loggerFactory.CreateLogger<SolidWorksConnection>(),
            settings);

        var adapter = new SolidWorks2023Adapter(
            loggerFactory.CreateLogger<SolidWorks2023Adapter>(),
            connection,
            settings,
            loggerFactory);

        _output.WriteLine("Testing SolidWorks connection...");

        // Act - Try to create a document (this will trigger connection)
        var result = await adapter.ExecuteAsync(
            "Document.CreateDocument",
            new Dictionary<string, object?> { ["Type"] = 1 },
            default);

        // Assert
        _output.WriteLine($"Result Success: {result.Success}");
        _output.WriteLine($"Result Message: {result.Message}");
        if (result.Data != null)
        {
            _output.WriteLine($"Result Data: {result.Data}");
        }

        Assert.True(result.Success, $"Failed to connect to SolidWorks: {result.Message}");
    }

    [Fact]
    public async Task CreateDocument_AndVerify()
    {
        if (!IntegrationTestGate.IsEnabled(_output))
        {
            return;
        }

        // Arrange
        var settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 3, ResetTimeoutSeconds = 30 }
        };

        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var circuitBreaker = new CircuitBreaker(settings, loggerFactory.CreateLogger<CircuitBreaker>());
        var connection = new SolidWorksConnection(loggerFactory.CreateLogger<SolidWorksConnection>(), settings);
        var adapter = new SolidWorks2023Adapter(
            loggerFactory.CreateLogger<SolidWorks2023Adapter>(),
            connection,
            settings,
            loggerFactory);

        _output.WriteLine("Creating new SolidWorks document...");

        // Act
        var result = await adapter.ExecuteAsync(
            "Document.CreateDocument",
            new Dictionary<string, object?> { ["Type"] = 1 },
            default);

        // Assert and Output
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Message: {result.Message}");

        if (result.Success && result.Data != null)
        {
            _output.WriteLine("Document created successfully!");
            _output.WriteLine($"Data: {result.Data}");
        }
        else
        {
            _output.WriteLine($"Failed: {result.Message}");
        }

        Assert.True(result.Success);
    }
}
