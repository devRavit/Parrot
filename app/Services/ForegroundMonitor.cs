using System.Windows.Threading;
using static Parrot.Native;

namespace Parrot;

/// <summary>Raises TargetChanged when the process owning the window UNDER THE CURSOR changes
/// (that's the process actually drawing the cursor there). Combines a WinEvent foreground hook
/// with a light poll so entering a game's render child is detected.</summary>
internal sealed class ForegroundMonitor : IForegroundMonitor
{
    public event Action<ProcessTarget>? TargetChanged;

    private IntPtr _winEvent;
    private WinEventDelegate? _winProc;
    private readonly DispatcherTimer _timer;
    private string _lastExe = "";

    public ForegroundMonitor()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        _winProc = OnWinEvent;
        _winEvent = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _winProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _timer.Start();
        Poll(force: true);
    }

    public void Stop()
    {
        _timer.Stop();
        try { if (_winEvent != IntPtr.Zero) UnhookWinEvent(_winEvent); } catch { }
        _winEvent = IntPtr.Zero;
    }

    public ProcessTarget Current()
    {
        IntPtr hwnd = IntPtr.Zero;
        if (GetCursorPos(out POINT p)) hwnd = WindowFromPoint(p);
        if (hwnd == IntPtr.Zero) hwnd = GetForegroundWindow();
        string exe = hwnd != IntPtr.Zero ? GetProcessExe(hwnd) : "";
        GetWindowThreadProcessId(hwnd, out uint pid);
        return new ProcessTarget(exe, hwnd, pid);
    }

    /// <summary>Force the next poll to re-emit even if the exe hasn't changed (e.g. after settings change).</summary>
    public void Invalidate() => _lastExe = "";

    private void OnWinEvent(IntPtr h, uint ev, IntPtr hwnd, int a, int b, uint c, uint d)
    {
        var disp = System.Windows.Application.Current?.Dispatcher;
        if (disp != null) disp.BeginInvoke(() => Poll());
        else Poll();
    }

    private void Poll(bool force = false)
    {
        var t = Current();
        if (t.Exe.Length == 0) return;
        if (!force && t.Exe == _lastExe) return;
        _lastExe = t.Exe;
        TargetChanged?.Invoke(t);
    }

    public void Dispose() => Stop();
}
