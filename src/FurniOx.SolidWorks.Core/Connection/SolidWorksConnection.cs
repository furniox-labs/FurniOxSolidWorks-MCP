using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Connection;

/// <summary>
/// COM helper methods for getting active objects
/// </summary>
internal static class ComHelper
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    public static object? GetActiveObject(string progId)
    {
        var clsid = Type.GetTypeFromProgID(progId)?.GUID ?? Guid.Empty;
        if (clsid == Guid.Empty)
        {
            return null;
        }

        GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
        return obj;
    }
}

/// <summary>
/// Manages COM connection to SolidWorks application
/// </summary>
public sealed class SolidWorksConnection : IDisposable
{
    private readonly ILogger<SolidWorksConnection> _logger;
    private readonly SolidWorksSettings _settings;
    private SldWorks? _swApp;
    private bool _disposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public SolidWorksConnection(ILogger<SolidWorksConnection> logger, SolidWorksSettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    /// <summary>
    /// Gets the SolidWorks application instance
    /// </summary>
    public SldWorks? Application => _swApp;

    /// <summary>
    /// Indicates whether connected to SolidWorks
    /// </summary>
    public bool IsConnected => _swApp != null;

    /// <summary>
    /// Connect to running SolidWorks instance or start new one
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        bool lockAcquired = false;
        try
        {
            await _connectionLock.WaitAsync(cancellationToken);
            lockAcquired = true;  // Lock acquired successfully

            // Check if existing connection is still valid
            if (_swApp != null)
            {
                if (CheckHealthInternal())
                {
                    return true;
                }
                else
                {
                    // Connection is stale, clear it and reconnect
                    _logger.LogWarning("Cached SolidWorks connection is stale, reconnecting...");
                    _swApp = null;
                }
            }

            var progIds = BuildProgIds();
            if (progIds.Count == 0)
            {
                _logger.LogError("No SolidWorks ProgIDs available for connection");
                return false;
            }

            _swApp = TryGetRunningInstance(progIds);

            if (_swApp == null)
            {
                _swApp = TryCreateInstance(progIds);
            }

            if (_swApp != null)
            {
                _logger.LogInformation("Connected to SolidWorks {Revision}", _swApp.RevisionNumber());
                return true;
            }

            _logger.LogWarning("Failed to connect to SolidWorks");
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SolidWorks connection attempt canceled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to SolidWorks");
            return false;
        }
        finally
        {
            if (lockAcquired)
            {
                _connectionLock.Release();
            }
        }
    }

    /// <summary>
    /// Attach to an already running SolidWorks instance without creating a new process.
    /// Used by bridge bootstrap so startup does not launch SolidWorks implicitly.
    /// </summary>
    public async Task<bool> AttachAsync(CancellationToken cancellationToken = default)
    {
        bool lockAcquired = false;
        try
        {
            await _connectionLock.WaitAsync(cancellationToken);
            lockAcquired = true;

            if (_swApp != null)
            {
                if (CheckHealthInternal())
                {
                    return true;
                }

                _logger.LogWarning("Cached SolidWorks connection is stale, reattaching to a running instance...");
                _swApp = null;
            }

            var progIds = BuildProgIds();
            if (progIds.Count == 0)
            {
                _logger.LogError("No SolidWorks ProgIDs available for connection");
                return false;
            }

            _swApp = TryGetRunningInstance(progIds);
            if (_swApp != null)
            {
                _logger.LogInformation("Attached to running SolidWorks {Revision}", _swApp.RevisionNumber());
                return true;
            }

            _logger.LogDebug("No running SolidWorks instance available to attach.");
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SolidWorks attach attempt canceled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching to running SolidWorks");
            return false;
        }
        finally
        {
            if (lockAcquired)
            {
                _connectionLock.Release();
            }
        }
    }

    private List<string> BuildProgIds()
    {
        var progIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (seen.Add(candidate))
            {
                progIds.Add(candidate);
            }
        }

        Add("SldWorks.Application");

        var versionHint = _settings.GetProgIdVersionHint();
        if (!string.IsNullOrEmpty(versionHint))
        {
            if (int.TryParse(versionHint, out var parsedVersion) && parsedVersion >= 1900)
            {
                _logger.LogWarning(
                    "SolidWorks ProgID version hint '{VersionHint}' looks like an installation year. " +
                    "Skipping version-specific ProgIDs and relying on generic discovery.",
                    versionHint);
                return progIds;
            }

            Add($"SldWorks.Application.{versionHint}");

            var digitsOnly = new string(versionHint.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
            if (!string.IsNullOrEmpty(digitsOnly))
            {
                Add($"SldWorks.Application.{digitsOnly}");
                if (!digitsOnly.Contains('.'))
                {
                    Add($"SldWorks.Application.{digitsOnly}.0");
                }
            }
        }

        return progIds;
    }

    private SldWorks? TryGetRunningInstance(IReadOnlyList<string> progIds)
    {
        foreach (var progId in progIds)
        {
            try
            {
                var obj = ComHelper.GetActiveObject(progId);
                if (obj is SldWorks app)
                {
                    return app;
                }
            }
            catch (COMException)
            {
                // Try next ProgID
            }
        }

        return null;
    }

    private SldWorks? TryCreateInstance(IReadOnlyList<string> progIds)
    {
        foreach (var progId in progIds)
        {
            var type = Type.GetTypeFromProgID(progId);
            if (type == null)
            {
                continue;
            }

            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance is SldWorks app)
                {
                    app.Visible = true; // Ensure visible
                    return app;
                }
            }
            catch (COMException)
            {
                // Try next ProgID
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating SolidWorks instance");
            }
        }

        return null;
    }

    /// <summary>
    /// Disconnect from SolidWorks
    /// </summary>
    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_swApp != null)
            {
                _logger.LogInformation("Disconnecting from SolidWorks");

                // Set to null and let GC handle COM cleanup
                // DO NOT use Marshal.ReleaseComObject on ISldWorks - causes crashes
                _swApp = null;

                // Force garbage collection for COM objects
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                _logger.LogInformation("Disconnected from SolidWorks");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from SolidWorks");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Check if connection is healthy (internal, no logging)
    /// </summary>
    private bool CheckHealthInternal()
    {
        if (_swApp == null)
        {
            return false;
        }

        try
        {
            // Try to call a simple method to verify connection
            _ = _swApp.RevisionNumber();
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidComObjectException)
        {
            return false;
        }
    }

    /// <summary>
    /// Check if connection is healthy
    /// </summary>
    public bool CheckHealth()
    {
        if (!CheckHealthInternal())
        {
            _logger.LogWarning("SolidWorks connection is unhealthy");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Returns lightweight connection info suitable for MCP resource serialization.
    /// No SolidWorks interop types are exposed in the return value.
    /// </summary>
    public (bool Connected, bool Healthy, string? Revision, bool? Visible) GetConnectionInfo()
    {
        if (_swApp == null)
        {
            return (false, false, null, null);
        }

        var healthy = CheckHealth();
        string? revision = null;
        bool? visible = null;

        try { revision = _swApp.RevisionNumber(); } catch { }
        try { visible = _swApp.Visible; } catch { }

        return (true, healthy, revision, visible);
    }

    /// <summary>
    /// Returns lightweight active document info suitable for MCP resource serialization.
    /// No SolidWorks interop types are exposed in the return value.
    /// </summary>
    public (bool HasDocument, string? Title, string? Path, string? TypeName, int TypeCode, bool Saved, bool ReadOnly, string? ErrorReason) GetActiveDocumentInfo()
    {
        if (_swApp == null)
        {
            return (false, null, null, null, 0, false, false, "Not connected");
        }

        dynamic? model = _swApp.ActiveDoc;
        if (model == null)
        {
            return (false, null, null, null, 0, false, false, "No document open");
        }

        try
        {
            int typeCode = (int)model.GetType();
            string typeName = typeCode switch
            {
                1 => "Part",
                2 => "Assembly",
                3 => "Drawing",
                _ => $"Unknown({typeCode})"
            };

            return (
                true,
                (string?)model.GetTitle(),
                (string?)model.GetPathName(),
                typeName,
                typeCode,
                !(bool)model.GetSaveFlag(),
                (bool)model.IsOpenedReadOnly(),
                null);
        }
        catch (Exception ex)
        {
            return (false, null, null, null, 0, false, false, $"Error reading document info: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Synchronous disconnect to avoid deadlock from GetAwaiter().GetResult()
        if (_swApp != null)
        {
            _logger.LogInformation("Disconnecting from SolidWorks (sync dispose)");
            _swApp = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _logger.LogInformation("Disconnected from SolidWorks");
        }

        _connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
