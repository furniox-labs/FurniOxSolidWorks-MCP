using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FurniOx.SolidWorks.Core.DocManager;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis.Assembly;

/// <summary>
/// Extracts custom properties, configuration properties, and summary info
/// for every unique component document in an assembly.
/// </summary>
internal static class AssemblyComponentPropertySupport
{
    internal static (Dictionary<string, ComponentDocumentProperties> Properties, List<string> SkippedPaths)
        ExtractComponentProperties(
            ISldWorks app,
            ILogger logger,
            Dictionary<string, IComponent2> componentIndex,
            bool openReferencedDocs,
            IDocumentPropertyReader? propertyReader = null)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, ComponentDocumentProperties>(StringComparer.OrdinalIgnoreCase);
        var skippedPaths = new List<string>();
        var openedCount = 0;

        // Collect unique file paths -> one representative component
        var uniquePaths = new Dictionary<string, IComponent2>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in componentIndex.Values)
        {
            var filePath = component.GetPathName();
            if (string.IsNullOrEmpty(filePath)) continue;

            try
            {
                var normalized = Path.GetFullPath(filePath);
                uniquePaths.TryAdd(normalized, component);
            }
            catch
            {
                // Invalid path characters or other path issues - skip
            }
        }

        logger.LogInformation(
            "ExtractComponentProperties: {TotalComponents} components -> {UniqueDocuments} unique documents (dedup took {ElapsedMs}ms)",
            componentIndex.Count, uniquePaths.Count, sw.ElapsedMilliseconds);

        var processed = 0;
        foreach (var (filePath, component) in uniquePaths)
        {
            try
            {
                var model = component.GetModelDoc2() as ModelDoc2;
                var openedByUs = false;
                if (model == null)
                {
                    if (!openReferencedDocs)
                    {
                        skippedPaths.Add(filePath);
                        continue;
                    }

                    // Try IDocumentPropertyReader first (DocManager at ~50ms, or OpenDoc6 fallback)
                    if (propertyReader != null && propertyReader.IsAvailable)
                    {
                        try
                        {
                            var readerResult = propertyReader.ReadPropertiesAsync(filePath).GetAwaiter().GetResult();
                            if (readerResult != null)
                            {
                                result[filePath] = readerResult;
                                processed++;
                                if (processed % 50 == 0)
                                {
                                    logger.LogInformation(
                                        "ExtractComponentProperties: processed {Processed}/{Total} documents via {Reader} ({ElapsedMs}ms)",
                                        processed, uniquePaths.Count, propertyReader.ReaderName, sw.ElapsedMilliseconds);
                                }
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "PropertyReader failed for {Path}, falling back to OpenDoc6", filePath);
                        }
                    }

                    var docType = InferDocumentType(filePath);
                    model = AnalysisDocumentSupport.TryOpenModelIfNeeded(app, filePath, docType, out openedByUs);
                    if (openedByUs && model != null)
                    {
                        openedCount++;
                        // Hide the document so it doesn't clutter the SolidWorks GUI
                        try { model.Visible = false; } catch { }
                    }
                }

                if (model == null)
                {
                    skippedPaths.Add(filePath);
                    continue;
                }

                var customProps = AnalysisExtractionSupport.ExtractCustomProperties(model, logger);
                var configProps = AnalysisExtractionSupport.ExtractConfigurationCustomProperties(model, logger);
                var summaryInfo = AnalysisExtractionSupport.ExtractSummaryInfo(model, logger);

                // Close document immediately after reading to free memory and avoid GUI clutter
                if (openedByUs)
                {
                    try { app.CloseDoc(model.GetTitle()); } catch { }
                }

                result[filePath] = new ComponentDocumentProperties
                {
                    CustomProperties = customProps,
                    ConfigurationCustomProperties = configProps.Count > 0 ? configProps : null,
                    SummaryInfo = summaryInfo
                };

                processed++;
                if (processed % 50 == 0)
                {
                    logger.LogInformation(
                        "ExtractComponentProperties: processed {Processed}/{Total} documents ({ElapsedMs}ms)",
                        processed, uniquePaths.Count, sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract properties for component: {Path}", filePath);
                skippedPaths.Add(filePath);
            }
        }

        logger.LogInformation(
            "ExtractComponentProperties complete: {Extracted} extracted, {Skipped} skipped, {Opened} opened by us ({ElapsedMs}ms total)",
            result.Count, skippedPaths.Count, openedCount, sw.ElapsedMilliseconds);

        return (result, skippedPaths);
    }

    private static swDocumentTypes_e InferDocumentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToUpperInvariant();
        return extension switch
        {
            ".SLDASM" => swDocumentTypes_e.swDocASSEMBLY,
            ".SLDDRW" => swDocumentTypes_e.swDocDRAWING,
            _ => swDocumentTypes_e.swDocPART
        };
    }
}
