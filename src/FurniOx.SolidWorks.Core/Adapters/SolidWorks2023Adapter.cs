using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Document;
using FurniOx.SolidWorks.Core.Adapters.Features;
using FurniOx.SolidWorks.Core.Adapters.SketchAdvanced;
using FurniOx.SolidWorks.Core.Adapters.SketchGeometry;
using FurniOx.SolidWorks.Core.Adapters.SketchInspection;
using FurniOx.SolidWorks.Core.Adapters.SketchParametric;
using FurniOx.SolidWorks.Core.Adapters.SketchProductivity;
using FurniOx.SolidWorks.Core.Adapters.SketchSpecialized;
using FurniOx.SolidWorks.Core.Adapters.Sorting;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters;

/// <summary>
/// Public SolidWorks adapter - handles the open-source MCP surface only.
/// Private analysis, metadata, governance, and bridge-backed flows are layered
/// in a separate private adapter/project.
/// </summary>
public sealed class SolidWorks2023Adapter : ISolidWorksAdapter, IDisposable
{
    private readonly DocumentOperations _documentOps;
    private readonly ExportOperations _exportOps;
    private readonly AssemblyBrowserOperations _assemblyBrowserOps;
    private readonly SketchOperations _sketchOps;
    private readonly FeatureOperations _featureOps;
    private readonly ConfigurationOperations _configOps;
    private readonly SelectionOperations _selectionOps;
    private readonly SortingOperations _sortingOps;
    private readonly SolidWorksConnection _connection;

    public SolidWorks2023Adapter(
        ILogger<SolidWorks2023Adapter> logger,
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        _connection = connection;

        _documentOps = CreateDocumentOperations(connection, settings, loggerFactory);
        _exportOps = new ExportOperations(connection, settings, loggerFactory.CreateLogger<ExportOperations>());
        _assemblyBrowserOps = new AssemblyBrowserOperations(connection, settings, loggerFactory.CreateLogger<AssemblyBrowserOperations>());
        _sketchOps = CreateSketchOperations(connection, settings, loggerFactory);
        _featureOps = CreateFeatureOperations(connection, settings, loggerFactory);
        _configOps = new ConfigurationOperations(connection, settings, loggerFactory);
        _selectionOps = new SelectionOperations(connection, settings, loggerFactory);
        _sortingOps = CreateSortingOperations(connection, settings, loggerFactory);
    }

    public bool CanHandle(string operation) => SolidWorksOperationCatalog.Known.Contains(operation);

    public async Task<ExecutionResult> ExecuteAsync(string operation, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        if (!_connection.IsConnected)
        {
            var connected = await _connection.ConnectAsync(cancellationToken);
            if (!connected)
            {
                return ExecutionResult.Failure("Failed to connect to SolidWorks");
            }
        }

        return operation switch
        {
            var op when op.StartsWith("Document.") => await _documentOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Export.") => await _exportOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("AssemblyBrowser.") => await _assemblyBrowserOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Configuration.") => await _configOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Selection.") => await _selectionOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Sketch.") => await _sketchOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Feature.") => await _featureOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Sorting.") => await _sortingOps.ExecuteAsync(operation, parameters, cancellationToken),
            _ => ExecutionResult.Failure($"Operation not implemented: {operation}")
        };
    }

    public void Dispose()
    {
        // Connection lifetime is owned by DI.
    }

    private static DocumentOperations CreateDocumentOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        var fileOperations = new DocumentFileOperations(connection, settings, loggerFactory.CreateLogger<DocumentFileOperations>());
        var sessionOperations = new DocumentSessionOperations(connection, settings, loggerFactory.CreateLogger<DocumentSessionOperations>());
        var lifecycleOperations = new DocumentLifecycleOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<DocumentLifecycleOperations>(),
            fileOperations,
            sessionOperations);

        var queryOperations = new DocumentQueryOperations(connection, settings, loggerFactory.CreateLogger<DocumentQueryOperations>());

        return new DocumentOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<DocumentOperations>(),
            lifecycleOperations,
            queryOperations);
    }

    private static SketchOperations CreateSketchOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        var sketchSessionGeometry = new SketchSessionGeometryOperations(connection, settings, loggerFactory.CreateLogger<SketchSessionGeometryOperations>());
        var sketchPrimitiveGeometry = new SketchPrimitiveGeometryOperations(connection, settings, loggerFactory.CreateLogger<SketchPrimitiveGeometryOperations>());
        var sketchArcGeometry = new SketchArcGeometryOperations(connection, settings, loggerFactory.CreateLogger<SketchArcGeometryOperations>());
        var sketchShapeGeometry = new SketchShapeGeometryOperations(connection, settings, loggerFactory.CreateLogger<SketchShapeGeometryOperations>());
        var sketchGeometry = new SketchGeometryOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SketchGeometryOperations>(),
            sketchSessionGeometry,
            sketchPrimitiveGeometry,
            sketchArcGeometry,
            sketchShapeGeometry);
        var sketchSegmentInspection = new SketchSegmentInspectionOperations(connection, settings, loggerFactory.CreateLogger<SketchSegmentInspectionOperations>());
        var sketchAnalysisInspection = new SketchAnalysisInspectionOperations(connection, settings, loggerFactory.CreateLogger<SketchAnalysisInspectionOperations>());
        var sketchInspection = new SketchInspectionOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SketchInspectionOperations>(),
            sketchSegmentInspection,
            sketchAnalysisInspection);
        var sketchConstraintOperations = new SketchConstraintOperations(connection, settings, loggerFactory.CreateLogger<SketchConstraintOperations>());
        var sketchDimensionOperations = new SketchDimensionOperations(connection, settings, loggerFactory.CreateLogger<SketchDimensionOperations>());
        var sketchParametric = new SketchParametricOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SketchParametricOperations>(),
            sketchConstraintOperations,
            sketchDimensionOperations);
        var sketchPattern = new SketchPatternOperations(connection, settings, loggerFactory.CreateLogger<SketchPatternOperations>());
        var sketchTransform = new SketchTransformOperations(connection, settings, loggerFactory.CreateLogger<SketchTransformOperations>());
        var sketchEditing = new SketchEditingOperations(connection, settings, loggerFactory.CreateLogger<SketchEditingOperations>());
        var sketchProductivity = new SketchProductivityOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SketchProductivityOperations>(),
            sketchPattern,
            sketchTransform,
            sketchEditing);
        var sketchCorner = new SketchCornerOperations(connection, settings, loggerFactory.CreateLogger<SketchCornerOperations>());
        var sketchSlot = new SketchSlotOperations(connection, settings, loggerFactory.CreateLogger<SketchSlotOperations>());
        var sketchThreeDimensional = new SketchThreeDimensionalOperations(connection, settings, loggerFactory.CreateLogger<SketchThreeDimensionalOperations>());
        var sketchCurve = new SketchCurveOperations(connection, settings, loggerFactory.CreateLogger<SketchCurveOperations>());
        var sketchAdvanced = new SketchAdvancedOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SketchAdvancedOperations>(),
            sketchCorner,
            sketchSlot,
            sketchThreeDimensional,
            sketchCurve);
        var sketchBlockOperations = new SketchBlockOperations(connection, settings, loggerFactory.CreateLogger<SketchBlockOperations>());
        var sketchConstraintManagementOperations = new SketchConstraintManagementOperations(connection, settings, loggerFactory.CreateLogger<SketchConstraintManagementOperations>());
        var sketchTextOperations = new SketchTextOperations(connection, settings, loggerFactory.CreateLogger<SketchTextOperations>());
        var sketchSpecialized = new SketchSpecializedOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SketchSpecializedOperations>(),
            sketchBlockOperations,
            sketchConstraintManagementOperations,
            sketchTextOperations);

        return new SketchOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SketchOperations>(),
            sketchGeometry,
            sketchInspection,
            sketchParametric,
            sketchProductivity,
            sketchAdvanced,
            sketchSpecialized);
    }

    private static FeatureOperations CreateFeatureOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        var bossExtrusionOperations = new FeatureBossExtrusionOperations(connection, settings, loggerFactory.CreateLogger<FeatureBossExtrusionOperations>());
        var cutExtrusionOperations = new FeatureCutExtrusionOperations(connection, settings, loggerFactory.CreateLogger<FeatureCutExtrusionOperations>());
        var revolveOperations = new FeatureRevolveOperations(connection, settings, loggerFactory.CreateLogger<FeatureRevolveOperations>());
        var filletOperations = new FeatureFilletOperations(connection, settings, loggerFactory.CreateLogger<FeatureFilletOperations>());
        var shellOperations = new FeatureShellOperations(connection, settings, loggerFactory.CreateLogger<FeatureShellOperations>());

        return new FeatureOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<FeatureOperations>(),
            bossExtrusionOperations,
            cutExtrusionOperations,
            revolveOperations,
            filletOperations,
            shellOperations);
    }

    private static SortingOperations CreateSortingOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        var componentReorderOperations = new SortingComponentReorderOperations(connection, settings, loggerFactory.CreateLogger<SortingComponentReorderOperations>());
        var featureReorderOperations = new SortingFeatureReorderOperations(connection, settings, loggerFactory.CreateLogger<SortingFeatureReorderOperations>());
        var inspectionOperations = new SortingInspectionOperations(connection, settings, loggerFactory.CreateLogger<SortingInspectionOperations>());

        return new SortingOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<SortingOperations>(),
            componentReorderOperations,
            featureReorderOperations,
            inspectionOperations);
    }
}
