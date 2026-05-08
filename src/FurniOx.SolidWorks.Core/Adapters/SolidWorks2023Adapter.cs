using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Adapters.Analysis;
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
/// Batch analysis, batch metadata, batch governance, and bridge-backed flows
/// are intentionally layered outside this adapter.
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
    private readonly AnalysisOperations _analysisOps;
    private readonly CustomPropertyOperations _customPropertyOps;
    private readonly SummaryInfoOperations _summaryInfoOps;
    private readonly CrossReferenceOperations _crossReferenceOps;
    private readonly EquationReferenceOperations _equationReferenceOps;
    private readonly DocumentRenameOperations _documentRenameOps;
    private readonly DocumentRenameAnywhereOperations _documentRenameAnywhereOps;
    private readonly DocumentRenameQueryOperations _documentRenameQueryOps;
    private readonly DocumentSuppressionOperations _documentSuppressionOps;
    private readonly DocumentReferenceReplacementOperations _documentReferenceReplacementOps;
    private readonly DocumentReferenceSearchPathOperations _documentReferenceSearchPathOps;
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
        _analysisOps = CreateAnalysisOperations(connection, settings, loggerFactory);
        _customPropertyOps = CreateCustomPropertyOperations(connection, settings, loggerFactory);
        _summaryInfoOps = new SummaryInfoOperations(connection, settings, loggerFactory.CreateLogger<SummaryInfoOperations>());
        _crossReferenceOps = new CrossReferenceOperations(connection, settings, loggerFactory.CreateLogger<CrossReferenceOperations>());
        _equationReferenceOps = new EquationReferenceOperations(connection, settings, loggerFactory.CreateLogger<EquationReferenceOperations>());
        _documentRenameOps = CreateDocumentRenameOperations(connection, settings, loggerFactory);
        _documentRenameAnywhereOps = new DocumentRenameAnywhereOperations(connection, settings, loggerFactory.CreateLogger<DocumentRenameAnywhereOperations>());
        _documentRenameQueryOps = new DocumentRenameQueryOperations(connection, settings, loggerFactory.CreateLogger<DocumentRenameQueryOperations>());
        _documentSuppressionOps = new DocumentSuppressionOperations(connection, settings, loggerFactory.CreateLogger<DocumentSuppressionOperations>());
        _documentReferenceReplacementOps = new DocumentReferenceReplacementOperations(connection, settings, loggerFactory.CreateLogger<DocumentReferenceReplacementOperations>());
        _documentReferenceSearchPathOps = new DocumentReferenceSearchPathOperations(connection, settings, loggerFactory.CreateLogger<DocumentReferenceSearchPathOperations>());
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
            var op when op.StartsWith("Export.") => await _exportOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("AssemblyBrowser.") => await _assemblyBrowserOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Configuration.") => await _configOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Selection.") => await _selectionOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Sketch.") => await _sketchOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Feature.") => await _featureOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Sorting.") => await _sortingOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Analysis.") => await _analysisOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("CustomProperty.") => await _customPropertyOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("SummaryInfo.") => await _summaryInfoOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when CrossReferenceOperationNames.All.Contains(op) => await _crossReferenceOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when EquationOperationNames.All.Contains(op) => await _equationReferenceOps.ExecuteAsync(operation, parameters, cancellationToken),
            DocumentGovernanceOperationNames.RenameDocument => await _documentRenameOps.ExecuteAsync(operation, parameters, cancellationToken),
            DocumentGovernanceOperationNames.RenameComponentFile => await _documentRenameOps.ExecuteAsync(operation, parameters, cancellationToken),
            DocumentGovernanceOperationNames.RenameComponentInstance => await _documentRenameOps.ExecuteAsync(operation, parameters, cancellationToken),
            DocumentGovernanceOperationNames.RenameComponentAnywhere => await _documentRenameAnywhereOps.ExecuteAsync(operation, parameters, cancellationToken),
            DocumentGovernanceOperationNames.GetRenamedDocuments => await _documentRenameQueryOps.ExecuteAsync(operation, parameters, cancellationToken),
            DocumentGovernanceOperationNames.DetectOrphanFiles => await _documentRenameQueryOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when DocumentSuppressionOperationNames.All.Contains(op) => await _documentSuppressionOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when DocumentReferenceReplacementOperationNames.All.Contains(op) => await _documentReferenceReplacementOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when DocumentReferenceSearchPathOperationNames.All.Contains(op) => await _documentReferenceSearchPathOps.ExecuteAsync(operation, parameters, cancellationToken),
            var op when op.StartsWith("Document.") => await _documentOps.ExecuteAsync(operation, parameters, cancellationToken),
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

    private static DocumentRenameOperations CreateDocumentRenameOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        var fileRenameOperations = new DocumentFileRenameOperations(connection, settings, loggerFactory.CreateLogger<DocumentFileRenameOperations>());
        var componentFileRenameOperations = new DocumentComponentFileRenameOperations(connection, settings, loggerFactory.CreateLogger<DocumentComponentFileRenameOperations>());
        var componentInstanceRenameOperations = new DocumentComponentInstanceRenameOperations(connection, settings, loggerFactory.CreateLogger<DocumentComponentInstanceRenameOperations>());

        return new DocumentRenameOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<DocumentRenameOperations>(),
            fileRenameOperations,
            componentFileRenameOperations,
            componentInstanceRenameOperations);
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

    private static AnalysisOperations CreateAnalysisOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        var partOps = new PartAnalysisOperations(connection, settings, loggerFactory.CreateLogger<PartAnalysisOperations>());
        var assemblyOps = new AssemblyAnalysisOperations(connection, settings, loggerFactory.CreateLogger<AssemblyAnalysisOperations>(), propertyReader: null);
        var drawingOps = new DrawingAnalysisOperations(connection, settings, loggerFactory.CreateLogger<DrawingAnalysisOperations>());

        return new AnalysisOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<AnalysisOperations>(),
            partOps,
            assemblyOps,
            drawingOps);
    }

    private static CustomPropertyOperations CreateCustomPropertyOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILoggerFactory loggerFactory)
    {
        var readOps = new CustomPropertyReadOperations(connection, settings, loggerFactory.CreateLogger<CustomPropertyReadOperations>());
        var writeOps = new CustomPropertyWriteOperations(connection, settings, loggerFactory.CreateLogger<CustomPropertyWriteOperations>());

        return new CustomPropertyOperations(
            connection,
            settings,
            loggerFactory.CreateLogger<CustomPropertyOperations>(),
            readOps,
            writeOps);
    }
}
