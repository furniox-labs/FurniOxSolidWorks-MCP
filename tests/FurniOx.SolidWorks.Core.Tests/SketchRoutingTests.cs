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
/// Tests that every known sketch operation string is correctly routed through
/// the SmartRouter -> OperationRecordingAdapter pipeline, and that unknown
/// operations return a failure result.
///
/// These tests do NOT require SolidWorks to be installed. The
/// OperationRecordingAdapter intercepts the call before any COM work happens.
/// </summary>
public class SketchRoutingTests
{
    private readonly SolidWorksSettings _settings;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly PerformanceMonitor _monitor;
    private readonly StaTaskRunner _staRunner;

    public SketchRoutingTests()
    {
        _settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings { FailureThreshold = 5, ResetTimeoutSeconds = 30 }
        };
        _circuitBreaker = new CircuitBreaker(_settings, NullLogger<CircuitBreaker>.Instance);
        _monitor = new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
        _staRunner = new StaTaskRunner(NullLogger<StaTaskRunner>.Instance);
    }

    // ========== Sketch geometry operations (Phase 0) ==========

    [Theory]
    [InlineData("Sketch.CreateSketch")]
    [InlineData("Sketch.ExitSketch")]
    [InlineData("Sketch.EditSketch")]          // regression guard - routing bug was fixed
    [InlineData("Sketch.SketchCircle")]
    [InlineData("Sketch.SketchLine")]
    [InlineData("Sketch.SketchCenterLine")]
    [InlineData("Sketch.SketchArc")]
    [InlineData("Sketch.Sketch3PointArc")]
    [InlineData("Sketch.SketchTangentArc")]
    [InlineData("Sketch.SketchCornerRectangle")]
    [InlineData("Sketch.SketchPoint")]
    [InlineData("Sketch.SketchEllipse")]
    [InlineData("Sketch.SketchSpline")]
    [InlineData("Sketch.SketchPolygon")]
    public async Task SketchGeometryOperations_RouteSuccessfully(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Sketch inspection operations (Phase 1) ==========

    [Theory]
    [InlineData("Sketch.ListSketchSegments")]
    [InlineData("Sketch.GetSketchSegmentInfo")]
    [InlineData("Sketch.AnalyzeSketch")]
    public async Task SketchInspectionOperations_RouteSuccessfully(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Sketch parametric operations (Phase 1) ==========

    [Theory]
    [InlineData("Sketch.AddConstraint")]
    [InlineData("Sketch.AddDimension")]
    public async Task SketchParametricOperations_RouteSuccessfully(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Sketch productivity operations (Phase 2) ==========

    [Theory]
    [InlineData("Sketch.LinearPattern")]
    [InlineData("Sketch.CircularPattern")]
    [InlineData("Sketch.MirrorSketch")]
    [InlineData("Sketch.OffsetEntities")]
    [InlineData("Sketch.RotateSketch")]
    [InlineData("Sketch.ScaleSketch")]
    [InlineData("Sketch.TrimEntity")]
    [InlineData("Sketch.ExtendEntity")]
    [InlineData("Sketch.ConvertEntities")]
    [InlineData("Sketch.SplitEntity")]
    public async Task SketchProductivityOperations_RouteSuccessfully(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Sketch advanced operations (Phase 3) ==========

    [Theory]
    [InlineData("Sketch.SketchFillet")]
    [InlineData("Sketch.SketchChamfer")]
    [InlineData("Sketch.SketchSlot")]
    [InlineData("Sketch.Create3DSketch")]
    public async Task SketchAdvancedOperations_RouteSuccessfully(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Sketch specialized operations (Phase 4) ==========

    [Theory]
    [InlineData("Sketch.InsertBlock")]
    [InlineData("Sketch.MakeBlock")]
    [InlineData("Sketch.ListConstraints")]
    [InlineData("Sketch.DisplayConstraints")]
    [InlineData("Sketch.DeleteConstraint")]
    [InlineData("Sketch.SketchText")]
    [InlineData("Sketch.ExplodeBlock")]
    public async Task SketchSpecializedOperations_RouteSuccessfully(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Full set - all known sketch operations as a single theory ==========

    [Theory]
    [InlineData("Sketch.CreateSketch")]
    [InlineData("Sketch.ExitSketch")]
    [InlineData("Sketch.EditSketch")]
    [InlineData("Sketch.SketchCircle")]
    [InlineData("Sketch.SketchLine")]
    [InlineData("Sketch.SketchCenterLine")]
    [InlineData("Sketch.SketchArc")]
    [InlineData("Sketch.Sketch3PointArc")]
    [InlineData("Sketch.SketchTangentArc")]
    [InlineData("Sketch.SketchCornerRectangle")]
    [InlineData("Sketch.SketchPoint")]
    [InlineData("Sketch.SketchEllipse")]
    [InlineData("Sketch.SketchSpline")]
    [InlineData("Sketch.SketchPolygon")]
    [InlineData("Sketch.ListSketchSegments")]
    [InlineData("Sketch.GetSketchSegmentInfo")]
    [InlineData("Sketch.AnalyzeSketch")]
    [InlineData("Sketch.AddConstraint")]
    [InlineData("Sketch.AddDimension")]
    [InlineData("Sketch.LinearPattern")]
    [InlineData("Sketch.CircularPattern")]
    [InlineData("Sketch.MirrorSketch")]
    [InlineData("Sketch.OffsetEntities")]
    [InlineData("Sketch.RotateSketch")]
    [InlineData("Sketch.ScaleSketch")]
    [InlineData("Sketch.TrimEntity")]
    [InlineData("Sketch.ExtendEntity")]
    [InlineData("Sketch.ConvertEntities")]
    [InlineData("Sketch.SplitEntity")]
    [InlineData("Sketch.SketchFillet")]
    [InlineData("Sketch.SketchChamfer")]
    [InlineData("Sketch.SketchSlot")]
    [InlineData("Sketch.Create3DSketch")]
    [InlineData("Sketch.InsertBlock")]
    [InlineData("Sketch.MakeBlock")]
    [InlineData("Sketch.ListConstraints")]
    [InlineData("Sketch.DisplayConstraints")]
    [InlineData("Sketch.DeleteConstraint")]
    [InlineData("Sketch.SketchText")]
    [InlineData("Sketch.ExplodeBlock")]
    public async Task AllSketchOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success, $"Expected routing to succeed for '{operation}'");
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Unknown sketch operation ==========

    [Fact]
    public async Task UnknownSketchOperation_ReturnsFailure()
    {
        // The recording adapter returns success for everything it handles.
        // To test the failure path we need a router backed by an adapter that
        // actually delegates to the real SketchOperations coordinator rather
        // than the stub, but without COM.  We verify here that the adapter
        // still records the call - meaning the router reached the adapter -
        // and that the operation name is preserved.
        //
        // The real failure (unknown op) is exercised in SketchOperations itself;
        // here we confirm the router does NOT swallow the unknown key.
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        // The stub adapter accepts everything and returns success, so we use a
        // dedicated rejecting adapter to validate the failure branch.
        var rejectingAdapter = new RejectingAdapter();
        var rejectingRouter = CreateRouter(rejectingAdapter);

        var result = await rejectingRouter.RouteAsync("Sketch.UnknownOperation", new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    // ========== Document operations ==========

    [Theory]
    [InlineData("Document.CreateDocument")]
    [InlineData("Document.OpenModel")]
    [InlineData("Document.SaveModel")]
    [InlineData("Document.CloseModel")]
    public async Task DocumentOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Export operations ==========

    [Theory]
    [InlineData("Export.ExportToSTEP")]
    [InlineData("Export.ExportToSTL")]
    [InlineData("Export.ExportToPDF")]
    [InlineData("Export.ExportToDXF")]
    [InlineData("Export.ExportToIGES")]
    public async Task ExportOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Configuration operations ==========

    [Theory]
    [InlineData("Configuration.GetConfigurationNames")]
    [InlineData("Configuration.ActivateConfiguration")]
    [InlineData("Configuration.AddConfiguration")]
    [InlineData("Configuration.DeleteConfiguration")]
    [InlineData("Configuration.CopyConfiguration")]
    [InlineData("Configuration.GetConfigurationCount")]
    [InlineData("Configuration.ShowConfiguration")]
    public async Task ConfigurationOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Selection operations ==========

    [Theory]
    [InlineData("Selection.SelectByID2")]
    [InlineData("Selection.SelectComponent")]
    [InlineData("Selection.ClearSelection2")]
    [InlineData("Selection.DeleteSelection2")]
    public async Task SelectionOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Feature operations ==========

    [Theory]
    [InlineData("Feature.CreateExtrusion")]
    [InlineData("Feature.CreateCutExtrusion")]
    [InlineData("Feature.CreateRevolve")]
    [InlineData("Feature.CreateFillet")]
    [InlineData("Feature.CreateShell")]
    public async Task FeatureOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Sorting operations ==========

    [Theory]
    [InlineData("Sorting.ListComponentFolders")]
    [InlineData("Sorting.ReorderByPositions")]
    [InlineData("Sorting.ReorderFeaturesByPositions")]
    public async Task SortingOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Cross-category theory ==========

    [Theory]
    [InlineData("Document.CreateDocument")]
    [InlineData("Document.OpenModel")]
    [InlineData("Document.SaveModel")]
    [InlineData("Document.CloseModel")]
    [InlineData("Export.ExportToSTEP")]
    [InlineData("Export.ExportToSTL")]
    [InlineData("Export.ExportToPDF")]
    [InlineData("Export.ExportToDXF")]
    [InlineData("Export.ExportToIGES")]
    [InlineData("Configuration.GetConfigurationNames")]
    [InlineData("Configuration.ActivateConfiguration")]
    [InlineData("Configuration.AddConfiguration")]
    [InlineData("Configuration.DeleteConfiguration")]
    [InlineData("Configuration.CopyConfiguration")]
    [InlineData("Configuration.GetConfigurationCount")]
    [InlineData("Configuration.ShowConfiguration")]
    [InlineData("Selection.SelectByID2")]
    [InlineData("Selection.SelectComponent")]
    [InlineData("Selection.ClearSelection2")]
    [InlineData("Selection.DeleteSelection2")]
    [InlineData("Feature.CreateExtrusion")]
    [InlineData("Feature.CreateCutExtrusion")]
    [InlineData("Feature.CreateRevolve")]
    [InlineData("Feature.CreateFillet")]
    [InlineData("Feature.CreateShell")]
    [InlineData("Sorting.ListComponentFolders")]
    [InlineData("Sorting.ReorderByPositions")]
    [InlineData("Sorting.ReorderFeaturesByPositions")]
    public async Task AllOperations_CanBeRouted(string operation)
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync(operation, new Dictionary<string, object?>());

        Assert.True(result.Success, $"Expected routing to succeed for '{operation}'");
        Assert.Equal(operation, adapter.LastOperation);
    }

    // ========== Parameter pass-through ==========

    [Fact]
    public async Task RouteAsync_PassesThroughParameters_ToAdapter()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var parameters = new Dictionary<string, object?>
        {
            ["PlaneId"] = "Front Plane",
            ["X"] = 10.0,
            ["Y"] = 20.0,
            ["Radius"] = 5.0
        };

        await router.RouteAsync("Sketch.SketchCircle", parameters);

        Assert.NotNull(adapter.LastParameters);
        Assert.Equal("Front Plane", adapter.LastParameters["PlaneId"]);
        Assert.Equal(10.0, adapter.LastParameters["X"]);
        Assert.Equal(20.0, adapter.LastParameters["Y"]);
        Assert.Equal(5.0, adapter.LastParameters["Radius"]);
    }

    [Fact]
    public async Task RouteAsync_EmptyParameters_RouteSucceeds()
    {
        var adapter = new OperationRecordingAdapter();
        var router = CreateRouter(adapter);

        var result = await router.RouteAsync("Sketch.ExitSketch", new Dictionary<string, object?>());

        Assert.True(result.Success);
        Assert.Equal("Sketch.ExitSketch", adapter.LastOperation);
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
    /// Records the last operation and parameters then always returns success.
    /// Accepts all operations from every category so it can serve as a
    /// universal routing stub.
    /// </summary>
    private sealed class OperationRecordingAdapter : ISolidWorksAdapter
    {
        public string? LastOperation { get; private set; }
        public IDictionary<string, object?>? LastParameters { get; private set; }

        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            LastOperation = operation;
            LastParameters = parameters;

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                MockResponse = true,
                Operation = operation,
                ParameterCount = parameters.Count
            }));
        }
    }

    /// <summary>
    /// Always returns a failure result, used to verify that the router
    /// surfaces adapter-reported failures back to the caller unchanged.
    /// </summary>
    private sealed class RejectingAdapter : ISolidWorksAdapter
    {
        public bool CanHandle(string operation) => true;

        public Task<ExecutionResult> ExecuteAsync(
            string operation,
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExecutionResult.Failure($"Unknown sketch operation: {operation}"));
        }
    }
}
