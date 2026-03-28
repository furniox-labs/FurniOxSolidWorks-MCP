using System;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Connection;

/// <summary>
/// Manages COM connection to SolidWorks application.
/// </summary>
public sealed class SolidWorksConnection : IDisposable
{
    private readonly ILogger<SolidWorksConnection> _logger;
    private readonly SolidWorksSettings _settings;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private SldWorks? _swApp;
    private bool _disposed;

    public SolidWorksConnection(ILogger<SolidWorksConnection> logger, SolidWorksSettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public SldWorks? Application => _swApp;

    public bool IsConnected => _swApp != null;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var lockAcquired = false;
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

                _logger.LogWarning("Cached SolidWorks connection is stale, reconnecting...");
                _swApp = null;
            }

            var progIds = SolidWorksConnectionFactory.BuildProgIds(_settings, _logger);
            if (progIds.Count == 0)
            {
                _logger.LogError("No SolidWorks ProgIDs available for connection");
                return false;
            }

            _swApp = SolidWorksConnectionFactory.TryGetRunningInstance(progIds)
                ?? SolidWorksConnectionFactory.TryCreateInstance(progIds, _logger);

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
    /// This keeps startup side effects explicit for callers that do not want to launch SolidWorks.
    /// </summary>
    public async Task<bool> AttachAsync(CancellationToken cancellationToken = default)
    {
        var lockAcquired = false;
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

            var progIds = SolidWorksConnectionFactory.BuildProgIds(_settings, _logger);
            if (progIds.Count == 0)
            {
                _logger.LogError("No SolidWorks ProgIDs available for connection");
                return false;
            }

            _swApp = SolidWorksConnectionFactory.TryGetRunningInstance(progIds);
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

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_swApp == null)
            {
                return;
            }

            _logger.LogInformation("Disconnecting from SolidWorks");
            _swApp = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            _logger.LogInformation("Disconnected from SolidWorks");
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

    public bool CheckHealth()
    {
        if (!CheckHealthInternal())
        {
            _logger.LogWarning("SolidWorks connection is unhealthy");
            return false;
        }

        return true;
    }

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
            var typeCode = (int)model.GetType();
            var typeName = typeCode switch
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

    private bool CheckHealthInternal()
    {
        if (_swApp == null)
        {
            return false;
        }

        try
        {
            _ = _swApp.RevisionNumber();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
