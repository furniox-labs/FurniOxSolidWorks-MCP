using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

/// <summary>
/// Handles assembly analysis operations.
/// </summary>
public class AssemblyAnalysisOperations : OperationHandlerBase
{
    private readonly DocManager.IDocumentPropertyReader? _propertyReader;

    public AssemblyAnalysisOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<AssemblyAnalysisOperations> logger,
        DocManager.IDocumentPropertyReader? propertyReader = null)
        : base(connection, settings, logger)
    {
        _propertyReader = propertyReader;
    }

    public override async Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            "Analysis.AnalyzeAssembly" => await AnalyzeAssemblyAsync(parameters, cancellationToken),
            _ => ExecutionResult.Failure($"Unknown assembly analysis operation: {operation}")
        };
    }

    internal sealed record AssemblyAnalysisOptions(
        string Fields,
        bool IncludeFeatures,
        bool IncludeComponents,
        bool IncludeMates,
        bool IncludeMassProperties,
        bool IncludeCustomProperties,
        bool IncludeHierarchy,
        bool IsTopLevel,
        bool IncludeTree,
        string? PathFilter,
        string? NamePathFilter,
        bool IncludeSuppressed,
        bool IncludeHidden,
        bool IncludeEnvelope,
        bool IncludeVirtual,
        bool IncludeComponentFolders,
        bool OpenReferencedDocs,
        bool IncludeComponentProperties);

    private Task<ExecutionResult> AnalyzeAssemblyAsync(IDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = app.ActiveDoc as ModelDoc2;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        if (model is not AssemblyDoc assemblyDoc)
        {
            return Task.FromResult(ExecutionResult.Failure("Active document is not an assembly"));
        }

        var openReferencedDocs = GetBoolParam(parameters, "OpenReferencedDocs", true);
        ModelDoc2? openedDoc = null;
        var openedByUs = false;

        try
        {
            var targetModel = model;
            var targetAssembly = assemblyDoc;
            string? analyzedFromSelection = null;

            var selectionManager = model.SelectionManager as ISelectionMgr;
            if (selectionManager != null && selectionManager.GetSelectedObjectCount2(-1) > 0)
            {
                var selectedObject = selectionManager.GetSelectedObject6(1, -1);
                if (selectedObject is IComponent2 selectedComponent)
                {
                    var componentModel = selectedComponent.GetModelDoc2() as ModelDoc2;
                    if (componentModel == null && openReferencedDocs)
                    {
                        componentModel = AnalysisHelpers.TryOpenModelIfNeeded(
                            app,
                            selectedComponent.GetPathName(),
                            swDocumentTypes_e.swDocASSEMBLY,
                            out openedByUs);
                        openedDoc = componentModel;
                    }

                    if (componentModel != null && componentModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        targetModel = componentModel;
                        targetAssembly = (AssemblyDoc)componentModel;
                        analyzedFromSelection = selectedComponent.Name2;
                        _logger.LogInformation("Analyzing selected sub-assembly: {Name}", analyzedFromSelection);
                    }
                }
            }

            var fields = GetStringParam(parameters, "Fields", "standard").ToLowerInvariant();
            var pathFilter = GetStringParamNullable(parameters, "PathFilter");
            var namePathFilter = GetStringParamNullable(parameters, "NamePathFilter");
            var includeFeatures = GetBoolParam(parameters, "IncludeFeatures", true);
            var includeComponents = GetBoolParam(parameters, "IncludeComponents", true);
            var includeMates = GetBoolParam(parameters, "IncludeMates", true);
            var includeMassProperties = GetBoolParam(parameters, "IncludeMassProperties", true);
            var includeInterferenceCheck = GetBoolParam(parameters, "IncludeInterferenceCheck", false);
            var includeCustomProperties = GetBoolParam(parameters, "IncludeCustomProperties", true);
            var includeHierarchy = GetBoolParam(parameters, "IncludeHierarchy", true);
            var includeTree = GetBoolParam(parameters, "IncludeTree", false);
            var includeSuppressed = GetBoolParam(parameters, "IncludeSuppressed", true);
            var includeHidden = GetBoolParam(parameters, "IncludeHidden", true);
            var includeEnvelope = GetBoolParam(parameters, "IncludeEnvelope", true);
            var includeVirtual = GetBoolParam(parameters, "IncludeVirtual", true);
            var includeComponentFolders = GetBoolParam(parameters, "IncludeComponentFolders", false);
            var includeComponentProperties = GetBoolParam(parameters, "IncludeComponentProperties", false);

            if (fields == "minimal")
            {
                includeInterferenceCheck = false;
                includeComponentProperties = false;
            }

            var options = new AssemblyAnalysisOptions(
                fields,
                includeFeatures,
                includeComponents,
                includeMates,
                includeMassProperties,
                includeCustomProperties,
                includeHierarchy,
                IsTopLevel: analyzedFromSelection == null,
                IncludeTree: includeTree,
                PathFilter: pathFilter,
                NamePathFilter: namePathFilter,
                IncludeSuppressed: includeSuppressed,
                IncludeHidden: includeHidden,
                IncludeEnvelope: includeEnvelope,
                IncludeVirtual: includeVirtual,
                IncludeComponentFolders: includeComponentFolders,
                OpenReferencedDocs: openReferencedDocs,
                IncludeComponentProperties: includeComponentProperties);

            var result = AnalyzeAssemblyInternal(targetModel, targetAssembly, options);
            if (includeInterferenceCheck)
            {
                result = result with { InterferenceCheck = Assembly.AssemblyInterferenceSupport.Perform(targetAssembly, _logger) };
            }

            result = result with
            {
                AnalyzedFromSelection = analyzedFromSelection,
                PathFilter = pathFilter
            };

            var outputPath = GetStringParamNullable(parameters, "OutputPath");
            if (!string.IsNullOrEmpty(outputPath))
            {
                if (!IsPathSafe(outputPath, out var outputPathError))
                    return Task.FromResult(ExecutionResult.Failure($"Invalid output path: {outputPathError}"));

                try
                {
                    var json = JsonSerializer.Serialize(result, AnalysisJsonOptions);
                    File.WriteAllText(outputPath, json);

                    var summary = new
                    {
                        Status = "Success",
                        OutputFile = outputPath,
                        FileSizeBytes = new FileInfo(outputPath).Length,
                        Summary = new
                        {
                            AssemblyName = result.Metadata.Name,
                            TotalComponents = result.TotalComponentCount,
                            FilteredComponents = result.FilteredComponentCount,
                            TotalMates = result.Mates.Count,
                            PathFilter = result.PathFilter,
                            FieldsMode = result.FieldsMode
                        },
                        Message = $"Full analysis saved to {outputPath}. Use Read tool to view file contents."
                    };

                    return Task.FromResult(ExecutionResult.SuccessResult(summary, "Assembly analysis saved to file"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write assembly analysis to file: {Path}", outputPath);
                    return Task.FromResult(ExecutionResult.Failure($"Failed to write to file: {ex.Message}"));
                }
            }

            return Task.FromResult(ExecutionResult.SuccessResult(result));
        }
        finally
        {
            AnalysisHelpers.CloseDocIfOpenedByUs(app, openedDoc, openedByUs);
        }
    }

    internal AssemblyAnalysisResult AnalyzeAssemblyInternal(
        ModelDoc2 targetModel,
        AssemblyDoc targetAssembly,
        string fields,
        bool includeFeatures,
        bool includeComponents,
        bool includeMates,
        bool includeMassProperties,
        bool includeCustomProperties,
        bool includeHierarchy,
        bool isTopLevel,
        bool includeTree,
        bool includeComponentFolders = false)
    {
        var options = new AssemblyAnalysisOptions(
            fields,
            includeFeatures,
            includeComponents,
            includeMates,
            includeMassProperties,
            includeCustomProperties,
            includeHierarchy,
            isTopLevel,
            includeTree,
            PathFilter: null,
            NamePathFilter: null,
            IncludeSuppressed: true,
            IncludeHidden: true,
            IncludeEnvelope: true,
            IncludeVirtual: true,
            IncludeComponentFolders: includeComponentFolders,
            OpenReferencedDocs: includeComponentFolders,
            IncludeComponentProperties: false);

        return AnalyzeAssemblyInternal(targetModel, targetAssembly, options);
    }

    internal AssemblyAnalysisResult AnalyzeAssemblyInternal(
        ModelDoc2 targetModel,
        AssemblyDoc targetAssembly,
        AssemblyAnalysisOptions options)
    {
        var minimalMode = options.Fields == "minimal";

        // Build full recursive component index only when we need the full hierarchy.
        // This is the most expensive COM operation (~128s for 2859 components).
        var needsFullIndex = (options.IncludeComponents && options.IncludeHierarchy)
                             || options.IncludeComponentProperties;
        Dictionary<string, IComponent2>? componentIndex = needsFullIndex
            ? AnalysisHelpers.BuildComponentIndex(targetAssembly)
            : null;

        var result = new AssemblyAnalysisResult
        {
            Metadata = Assembly.AssemblyMetadataSupport.Extract(targetModel, targetAssembly, componentIndex)
        };
        result = result with { Metadata = result.Metadata with { IsTopLevel = options.IsTopLevel } };

        if (options.IncludeFeatures)
        {
            result = result with
            {
                Features = minimalMode
                    ? AnalysisHelpers.ExtractFeaturesMinimal(targetModel)
                    : AnalysisHelpers.ExtractFeatures(targetModel)
            };
        }

        var totalComponentCount = 0;
        if (options.IncludeComponents)
        {
            var (components, totalCount) = Assembly.AssemblyComponentSupport.ExtractFiltered(
                _connection.Application,
                _logger,
                targetModel,
                targetAssembly,
                options.IncludeHierarchy,
                options.PathFilter,
                options.NamePathFilter,
                options.IncludeSuppressed,
                options.IncludeHidden,
                options.IncludeEnvelope,
                options.IncludeVirtual,
                minimalMode,
                options.IncludeComponentFolders,
                options.OpenReferencedDocs,
                prebuiltIndex: componentIndex!);

            totalComponentCount = totalCount;
            result = result with
            {
                Components = components,
                Hierarchy = options.IncludeTree ? AnalysisHelpers.BuildHierarchyTree(components) : null
            };
        }

        if (options.IncludeMates)
        {
            result = result with { Mates = Assembly.AssemblyMateSupport.Extract(targetModel) };
        }

        if (options.IncludeMassProperties)
        {
            result = result with { MassProperties = AnalysisHelpers.ExtractMassProperties(targetModel, _logger) };
        }

        if (options.IncludeCustomProperties)
        {
            result = result with { CustomProperties = AnalysisHelpers.ExtractCustomProperties(targetModel, _logger) };

            if (options.Fields != "minimal")
            {
                var configProps = AnalysisExtractionSupport.ExtractConfigurationCustomProperties(targetModel, _logger);
                if (configProps.Count > 0)
                {
                    result = result with { ConfigurationCustomProperties = configProps };
                }
            }
        }

        if (options.Fields != "minimal")
        {
            result = result with { SummaryInfo = AnalysisExtractionSupport.ExtractSummaryInfo(targetModel, _logger) };
        }

        if (options.IncludeComponentProperties)
        {
            var (componentProps, skippedPaths) = Assembly.AssemblyComponentPropertySupport
                .ExtractComponentProperties(
                    _connection.Application!,
                    _logger,
                    componentIndex!,
                    options.OpenReferencedDocs,
                    _propertyReader);

            result = result with
            {
                ComponentProperties = componentProps.Count > 0 ? componentProps : null,
                SkippedPropertyPaths = skippedPaths.Count > 0 ? skippedPaths : null
            };
        }

        return result with
        {
            FieldsMode = options.Fields,
            TotalComponentCount = totalComponentCount,
            FilteredComponentCount = result.Components.Count
        };
    }
}
