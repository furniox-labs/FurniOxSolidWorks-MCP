using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.DocManager;

/// <summary>
/// Reads custom properties, configuration properties, and summary info from a SolidWorks document
/// by file path — without requiring the document to be open in SolidWorks.
///
/// Two implementations:
/// - DocManagerPropertyReader: Uses SolidWorks Document Manager API (~50ms/file, requires license key)
/// - FallbackPropertyReader: Uses OpenDoc6/CloseDoc (~4.8s/file, no extra license needed)
/// </summary>
public interface IDocumentPropertyReader
{
    /// <summary>
    /// Whether this reader is available (e.g., Document Manager license key configured).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Human-readable name for logging (e.g., "DocumentManager" or "OpenDoc6Fallback").
    /// </summary>
    string ReaderName { get; }

    /// <summary>
    /// Read properties from a file on disk without requiring it to be open in SolidWorks.
    /// </summary>
    /// <param name="filePath">Full path to .SLDPRT or .SLDASM file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Properties or null if the file couldn't be read</returns>
    Task<ComponentDocumentProperties?> ReadPropertiesAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
