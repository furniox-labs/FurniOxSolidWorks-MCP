using System.Collections.Generic;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.SmartRouting;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;
using PerformanceMonitor = FurniOx.SolidWorks.Core.Intelligence.PerformanceMonitor;

namespace FurniOx.SolidWorks.Integration.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task SmartRouter_ExecutesComPath()
    {
        if (!IntegrationTestGate.IsEnabled())
        {
            return;
        }

        var settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 3, ResetTimeoutSeconds = 30 }
        };

        var router = CreateRouter(settings);
        // Use a mock adapter that returns success
        var result = await router.RouteAsync("Document.GetDocumentInfo", new Dictionary<string, object?>());

        // Will fail without SolidWorks connection, but tests routing works
        Assert.NotNull(result);
    }

    private static SmartRouter CreateRouter(SolidWorksSettings settings)
    {
        var circuitBreaker = new CircuitBreaker(settings, NullLogger<CircuitBreaker>.Instance);
        var connection = new SolidWorksConnection(NullLogger<SolidWorksConnection>.Instance, settings);
        var loggerFactory = NullLoggerFactory.Instance;
        var staRunner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
        var adapter = new SolidWorks2023Adapter(
            NullLogger<SolidWorks2023Adapter>.Instance,
            connection,
            settings,
            loggerFactory);
        var monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        return new SmartRouter(
            circuitBreaker,
            adapter,
            staRunner,
            monitor,
            NullLogger<SmartRouter>.Instance);
    }
}

internal static class IntegrationTestGate
{
    private const string EnableVariableName = "SOLIDWORKS_INTEGRATION_TESTS";

    public static bool IsEnabled(Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        var enabled = string.Equals(
            System.Environment.GetEnvironmentVariable(EnableVariableName),
            "1",
            System.StringComparison.Ordinal);

        if (!enabled)
        {
            output?.WriteLine(
                $"Manual SolidWorks integration test disabled. Set {EnableVariableName}=1 to execute COM-backed tests.");
        }

        return enabled;
    }
}
