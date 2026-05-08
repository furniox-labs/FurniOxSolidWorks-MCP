using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// Closes the standard Win32 file Open picker (window class #32770) that
/// SolidWorks programmatically launches per missing reference during OpenDoc7
/// and Save3+SaveReferenced. The SW API has no reliable mechanism to suppress
/// it: ReferenceNotFoundNotify is documented but known-unreliable across versions
/// (see cadoverflow / eng-tips threads), and the swAssemblyOpenMessagesDismissTime
/// preference only governs SW's own status messages — not the system picker SW
/// launches when its resolver can't find a referenced file.
///
/// Approach: a background thread polls EnumWindows every ~50 ms during the
/// suppressed call. Any window that matches:
///   - parent process == the SW instance our COM connection talks to (GetProcessID)
///   - window class == "#32770" (Win32 dialog class)
///   - title == "Open" or starts with "Open " (locale-tolerant)
/// gets a PostMessage(WM_COMMAND, IDCANCEL) — the same effect as the user
/// pressing Cancel. SW then suppresses the corresponding component and moves
/// on to the next missing ref (if any).
///
/// Polling rather than a CBT hook because hooks must run on the SW UI thread,
/// which we don't own. Polling has measurable overhead but only during the
/// scoped call, and 50 ms is fast enough that the dialog never visibly blinks.
/// </summary>
internal sealed class CommonDialogCloser : IDisposable
{
    private const string DialogWindowClass = "#32770";
    private const uint WM_COMMAND = 0x0111;
    private const int IDCANCEL = 2;
    private const int PollIntervalMs = 50;

    private readonly uint _swProcessId;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _watcher;
    private readonly HashSet<IntPtr> _alreadyClosed = new();
    private readonly object _closedLock = new();
    private int _closedCount;
    private bool _disposed;

    public CommonDialogCloser(int swProcessId, ILogger logger)
    {
        _swProcessId = (uint)swProcessId;
        _logger = logger;
        _watcher = new Thread(WatchLoop)
        {
            IsBackground = true,
            Name = "FurniOx.MCP.DialogCloser"
        };
        _watcher.Start();
    }

    /// <summary>
    /// Number of dialogs auto-closed during the lifetime of this scope.
    /// Useful diagnostic for callers — tells them how many missing-ref
    /// dialogs they would otherwise have had to dismiss manually.
    /// </summary>
    public int ClosedCount
    {
        get
        {
            lock (_closedLock) return _closedCount;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _cts.Cancel();
            // Generous join timeout; the watcher only sleeps in 50 ms slices.
            _watcher.Join(500);
        }
        catch { }
        finally
        {
            _cts.Dispose();
        }
    }

    private void WatchLoop()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    EnumWindows(CloseIfMatches, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "EnumWindows iteration failed");
                }

                if (_cts.Token.WaitHandle.WaitOne(PollIntervalMs)) return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dialog watcher exited unexpectedly");
        }
    }

    private bool CloseIfMatches(IntPtr hWnd, IntPtr _)
    {
        try
        {
            if (GetWindowThreadProcessId(hWnd, out var pid) == 0) return true;
            if (pid != _swProcessId) return true;

            var classBuf = new StringBuilder(64);
            var classLen = GetClassName(hWnd, classBuf, classBuf.Capacity);
            if (classLen == 0) return true;
            if (classBuf.ToString() != DialogWindowClass) return true;

            var titleBuf = new StringBuilder(256);
            GetWindowText(hWnd, titleBuf, titleBuf.Capacity);
            var title = titleBuf.ToString();
            if (!IsOpenPickerTitle(title)) return true;

            lock (_closedLock)
            {
                if (!_alreadyClosed.Add(hWnd)) return true;
                _closedCount++;
            }

            // PostMessage(WM_COMMAND, IDCANCEL) is the documented way to dismiss
            // a common-dialog Cancel button — same wire-level effect as a click.
            PostMessage(hWnd, WM_COMMAND, new IntPtr(IDCANCEL), IntPtr.Zero);
        }
        catch
        {
            // Watchdog never propagates exceptions out of EnumWindows.
        }
        return true;
    }

    private static bool IsOpenPickerTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return false;
        // English-default; tolerate a trailing colon/space variant. If localised
        // SW is in scope later, we can extend with the matching translations.
        if (string.Equals(title, "Open", StringComparison.OrdinalIgnoreCase)) return true;
        if (title.StartsWith("Open ", StringComparison.OrdinalIgnoreCase)) return true;
        if (title.StartsWith("Open:", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
