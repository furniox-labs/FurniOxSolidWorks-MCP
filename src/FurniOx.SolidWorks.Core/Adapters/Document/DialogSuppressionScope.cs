using System;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Document;

/// <summary>
/// IDisposable scope bundling the safety-net layers that don't depend on
/// SW event subscriptions. The CALLER is expected to subscribe to the
/// missing-ref interception events (PromptForFilenameNotify primarily,
/// ReferencedFilePreNotify2 + ReferenceNotFoundNotify for diagnostics)
/// directly around their OpenDoc7/Save3 call — those are stateful per-call.
///
/// Three "background" layers managed here:
///   1. swExtRefNoPromptOrSave preference — suppresses read-only-ref save
///      prompts. Documented behaviour. Does NOT touch the missing-file picker.
///   2. swAssemblyOpenMessagesDismissTime preference (set to 1ms) — auto-
///      dismisses SW's own status messages. NOT a suppression for the system
///      Open picker (verified empirically: setting this did not stop the
///      missing-ref Win32 file picker). Kept because it's free and may help
///      on warm opens or other status messages.
///   3. CommonDialogCloser Win32 watchdog — polls EnumWindows during the
///      scoped call and PostMessages WM_COMMAND/IDCANCEL to any system Open
///      picker (#32770) owned by SW's process. Final safety net: when
///      PromptForFilenameNotify / ReferencedFilePreNotify2 either don't fire
///      or the SW API ignores their override, the watchdog still closes the
///      dialog before the user sees it. Reliable across SW versions.
///
/// On Dispose: stops the watchdog, restores both preferences. ClosedDialogCount
/// surfaces how many system pickers were auto-dismissed — useful diagnostic
/// signal for whether the event-based suppression worked or fell through to
/// the watchdog.
/// </summary>
internal sealed class DialogSuppressionScope : IDisposable
{
    private const int SilentDismissTimeMs = 1;

    private readonly SldWorks _app;
    private readonly ILogger _logger;

    private readonly bool _originalNoPrompt;
    private readonly bool _noPromptToggled;
    private readonly int _originalDismissTime;
    private readonly bool _dismissTimeToggled;
    private readonly CommonDialogCloser? _dialogCloser;
    private bool _disposed;

    public DialogSuppressionScope(SldWorks app, ILogger logger)
    {
        _app = app;
        _logger = logger;

        try
        {
            _originalNoPrompt = _app.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave);
            if (!_originalNoPrompt)
            {
                _app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave, true);
                _noPromptToggled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set swExtRefNoPromptOrSave; readonly-ref save prompt may still appear");
        }

        try
        {
            _originalDismissTime = _app.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAssemblyOpenMessagesDismissTime);
            if (_originalDismissTime != SilentDismissTimeMs)
            {
                _app.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAssemblyOpenMessagesDismissTime, SilentDismissTimeMs);
                _dismissTimeToggled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set swAssemblyOpenMessagesDismissTime; SW status messages may still appear");
        }

        // Win32 watchdog: needs SW's process ID so it only acts on dialogs owned
        // by the SW instance our COM connection is bound to (avoids interfering
        // with any other SW process the user has running).
        try
        {
            var pid = _app.GetProcessID();
            if (pid > 0)
            {
                _dialogCloser = new CommonDialogCloser(pid, _logger);
            }
            else
            {
                _logger.LogWarning("ISldWorks.GetProcessID returned {Pid}; system Open picker will NOT be auto-dismissed", pid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start CommonDialogCloser; system Open picker may block the call");
        }
    }

    /// <summary>
    /// How many system Open pickers were auto-dismissed during this scope —
    /// each one corresponds to a missing reference SW asked the user to locate.
    /// </summary>
    public int ClosedDialogCount => _dialogCloser?.ClosedCount ?? 0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop the watchdog FIRST so it isn't running while we restore prefs.
        try { _dialogCloser?.Dispose(); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to stop dialog closer"); }

        if (_dismissTimeToggled)
        {
            try
            {
                _app.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAssemblyOpenMessagesDismissTime, _originalDismissTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore swAssemblyOpenMessagesDismissTime");
            }
        }

        if (_noPromptToggled)
        {
            try
            {
                _app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave, _originalNoPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore swExtRefNoPromptOrSave");
            }
        }
    }
}
