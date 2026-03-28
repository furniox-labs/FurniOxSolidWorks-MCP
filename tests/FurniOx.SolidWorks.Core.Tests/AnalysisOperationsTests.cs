using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Core.SmartRouting;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;
using PerformanceMonitor = FurniOx.SolidWorks.Core.Intelligence.PerformanceMonitor;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Tests for AnalysisOperations to ensure all operations are correctly routed and handled.
/// These tests use mock adapters since actual SolidWorks is not available in CI.
/// </summary>
public class AnalysisOperationsTests
{
    private readonly SolidWorksSettings _settings;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly PerformanceMonitor _monitor;
    private readonly StaTaskRunner _staRunner;

    public AnalysisOperationsTests()
    {
        _settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 5, ResetTimeoutSeconds = 30 }
        };
        _circuitBreaker = new CircuitBreaker(_settings, NullLogger<CircuitBreaker>.Instance);
        _monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        _staRunner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
    }

    // ========== AnalyzePart Tests ==========

    [Fact]
    public async Task AnalyzePart_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["Fields"] = "standard",
            ["IncludeFeatures"] = true,
            ["IncludeMassProperties"] = true,
            ["IncludeBodies"] = true,
            ["IncludeCustomProperties"] = true,
            ["IncludeComponentFolders"] = false,
            ["OpenReferencedDocs"] = true
        };

        await router.RouteAsync("Analysis.AnalyzePart", parameters);

        Assert.Equal("Analysis.AnalyzePart", adapter.LastOperation);
        Assert.Equal("standard", adapter.LastParameters?["Fields"]);
        Assert.True((bool?)adapter.LastParameters?["IncludeFeatures"]);
    }

    [Fact]
    public async Task AnalyzePart_WithMinimalFields_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["Fields"] = "minimal",
            ["IncludeFeatures"] = false,
            ["IncludeMassProperties"] = false
        };

        await router.RouteAsync("Analysis.AnalyzePart", parameters);

        Assert.Equal("Analysis.AnalyzePart", adapter.LastOperation);
        Assert.Equal("minimal", adapter.LastParameters?["Fields"]);
    }

    [Fact]
    public async Task AnalyzePart_WithOutputPath_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["Fields"] = "full",
            ["OutputPath"] = "C:\\temp\\analysis.json"
        };

        await router.RouteAsync("Analysis.AnalyzePart", parameters);

        Assert.Equal("C:\\temp\\analysis.json", adapter.LastParameters?["OutputPath"]);
    }

    // ========== AnalyzeAssembly Tests ==========

    [Fact]
    public async Task AnalyzeAssembly_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["Fields"] = "standard",
            ["IncludeFeatures"] = true,
            ["IncludeComponents"] = true,
            ["IncludeMates"] = true,
            ["IncludeMassProperties"] = true,
            ["IncludeCustomProperties"] = true,
            ["IncludeHierarchy"] = true,
            ["IncludeTree"] = false,
            ["IncludeComponentFolders"] = false
        };

        await router.RouteAsync("Analysis.AnalyzeAssembly", parameters);

        Assert.Equal("Analysis.AnalyzeAssembly", adapter.LastOperation);
        Assert.True((bool?)adapter.LastParameters?["IncludeComponents"]);
        Assert.True((bool?)adapter.LastParameters?["IncludeMates"]);
    }

    [Fact]
    public async Task AnalyzeAssembly_WithPathFilter_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["PathFilter"] = "Projektai",
            ["NamePathFilter"] = "SubAssy-1",
            ["IncludeHierarchy"] = true
        };

        await router.RouteAsync("Analysis.AnalyzeAssembly", parameters);

        Assert.Equal("Projektai", adapter.LastParameters?["PathFilter"]);
        Assert.Equal("SubAssy-1", adapter.LastParameters?["NamePathFilter"]);
    }

    [Fact]
    public async Task AnalyzeAssembly_WithComponentFolders_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["IncludeComponentFolders"] = true,
            ["IncludeTree"] = true
        };

        await router.RouteAsync("Analysis.AnalyzeAssembly", parameters);

        Assert.True((bool?)adapter.LastParameters?["IncludeComponentFolders"]);
        Assert.True((bool?)adapter.LastParameters?["IncludeTree"]);
    }

    // ========== AnalyzeDrawing Tests ==========

    [Fact]
    public async Task AnalyzeDrawing_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["IncludeSheets"] = true,
            ["IncludeViews"] = true,
            ["IncludeAnnotations"] = true,
            ["IncludeBomTables"] = true,
            ["IncludeReferencedModels"] = true,
            ["IncludeCustomProperties"] = true
        };

        await router.RouteAsync("Analysis.AnalyzeDrawing", parameters);

        Assert.Equal("Analysis.AnalyzeDrawing", adapter.LastOperation);
        Assert.True((bool?)adapter.LastParameters?["IncludeSheets"]);
        Assert.True((bool?)adapter.LastParameters?["IncludeViews"]);
    }

    [Fact]
    public async Task AnalyzeDrawing_WithSelectiveOptions_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["IncludeSheets"] = true,
            ["IncludeViews"] = false,
            ["IncludeAnnotations"] = false,
            ["IncludeBomTables"] = true
        };

        await router.RouteAsync("Analysis.AnalyzeDrawing", parameters);

        Assert.True((bool?)adapter.LastParameters?["IncludeSheets"]);
        Assert.False((bool?)adapter.LastParameters?["IncludeViews"]);
        Assert.True((bool?)adapter.LastParameters?["IncludeBomTables"]);
    }

    // ========== GetMassProperties Tests ==========

    [Fact]
    public async Task GetMassProperties_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>();

        await router.RouteAsync("Analysis.GetMassProperties", parameters);

        Assert.Equal("Analysis.GetMassProperties", adapter.LastOperation);
    }

    // ========== AnalyzePartsBatch Tests ==========

    [Fact]
    public async Task AnalyzePartsBatch_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["InputPath"] = "C:\\temp\\input.json",
            ["OutputPath"] = "C:\\temp\\output.json",
            ["Fields"] = "standard",
            ["IncludeFeatures"] = true,
            ["IncludeMassProperties"] = true,
            ["IncludeBodies"] = true,
            ["IncludeCustomProperties"] = true,
            ["IncludeComponentFolders"] = false
        };

        await router.RouteAsync("Analysis.AnalyzePartsBatch", parameters);

        Assert.Equal("Analysis.AnalyzePartsBatch", adapter.LastOperation);
        Assert.Equal("C:\\temp\\input.json", adapter.LastParameters?["InputPath"]);
        Assert.Equal("C:\\temp\\output.json", adapter.LastParameters?["OutputPath"]);
    }

    [Fact]
    public async Task AnalyzePartsBatch_WithComponentFolders_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["InputPath"] = "C:\\temp\\input.json",
            ["OutputPath"] = "C:\\temp\\output.json",
            ["IncludeComponentFolders"] = true
        };

        await router.RouteAsync("Analysis.AnalyzePartsBatch", parameters);

        Assert.True((bool?)adapter.LastParameters?["IncludeComponentFolders"]);
    }

    // ========== AnalyzeAssembliesBatch Tests ==========

    [Fact]
    public async Task AnalyzeAssembliesBatch_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["InputPath"] = "C:\\temp\\assemblies_input.json",
            ["OutputPath"] = "C:\\temp\\assemblies_output.json",
            ["Fields"] = "standard",
            ["IncludeFeatures"] = true,
            ["IncludeComponents"] = true,
            ["IncludeMates"] = true,
            ["IncludeMassProperties"] = true,
            ["IncludeCustomProperties"] = true,
            ["IncludeHierarchy"] = true,
            ["IncludeTree"] = false,
            ["IncludeComponentFolders"] = false
        };

        await router.RouteAsync("Analysis.AnalyzeAssembliesBatch", parameters);

        Assert.Equal("Analysis.AnalyzeAssembliesBatch", adapter.LastOperation);
        Assert.Equal("C:\\temp\\assemblies_input.json", adapter.LastParameters?["InputPath"]);
    }

    [Fact]
    public async Task AnalyzeAssembliesBatch_WithTreeAndFolders_RoutesCorrectly()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["InputPath"] = "C:\\temp\\input.json",
            ["OutputPath"] = "C:\\temp\\output.json",
            ["IncludeTree"] = true,
            ["IncludeComponentFolders"] = true
        };

        await router.RouteAsync("Analysis.AnalyzeAssembliesBatch", parameters);

        Assert.True((bool?)adapter.LastParameters?["IncludeTree"]);
        Assert.True((bool?)adapter.LastParameters?["IncludeComponentFolders"]);
    }

    // ========== Operation Prefixes Tests ==========

    [Theory]
    [InlineData("Analysis.AnalyzePart")]
    [InlineData("Analysis.AnalyzeAssembly")]
    [InlineData("Analysis.AnalyzeDrawing")]
    [InlineData("Analysis.GetMassProperties")]
    [InlineData("Analysis.AnalyzePartsBatch")]
    [InlineData("Analysis.AnalyzeAssembliesBatch")]
    public async Task AllAnalysisOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Helper Methods ==========

    private SmartRouter CreateRouter(ISolidWorksAdapter adapter)
    {
        return new SmartRouter(
            _circuitBreaker,
            adapter,
            _staRunner,
            _monitor,
            NullLogger<SmartRouter>.Instance);
    }

    /// <summary>
    /// Mock adapter that records operations and parameters for verification
    /// </summary>
    private sealed class OperationRecordingAdapter : ISolidWorksAdapter
    {
        public string? LastOperation { get; private set; }
        public IDictionary<string, object?>? LastParameters { get; private set; }

        public bool CanHandle(string operation) => operation.StartsWith("Analysis.");

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            LastOperation = operation;
            LastParameters = parameters;

            // Return a mock success result
            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                MockResponse = true,
                Operation = operation,
                ParameterCount = parameters.Count
            }));
        }
    }
}
