using System.Drawing;

namespace Parrot;

/// <summary>Immutable request describing which cursor to produce.</summary>
internal readonly record struct CursorSpec(string Design, int Size, int Color, bool ReplaceAllTypes);

/// <summary>A rendered cursor bitmap plus its hotspot. Owns the bitmap.</summary>
internal sealed class CursorImage : IDisposable
{
    public Bitmap Bitmap { get; }
    public int HotX { get; }
    public int HotY { get; }
    public CursorImage(Bitmap bitmap, int hotX, int hotY) { Bitmap = bitmap; HotX = hotX; HotY = hotY; }
    public void Dispose() => Bitmap.Dispose();
}

/// <summary>Identifies the app the cursor is currently over/for.</summary>
internal readonly record struct ProcessTarget(string Exe, IntPtr Hwnd, uint Pid);

internal enum StrategyKind { System, Injection, Overlay }

// ---- rendering ----
internal interface ICursorProvider
{
    IReadOnlyList<CursorDef> Designs { get; }
    CursorImage Render(CursorSpec spec);
    Bitmap Preview(CursorDef def, int box, int colorIndex);
    void Reload();
}

// ---- low-level Win32 services (DIP) ----
internal interface ISystemCursorService
{
    bool Active { get; }
    void Apply(CursorImage image, bool replaceAllTypes);
    void Blank();
    void Reassert();
    void Restore();
}

internal interface IOverlayService : IDisposable
{
    bool Visible { get; }
    void Show(CursorImage image);
    void Update(CursorImage image);
    void Hide();
}

internal enum InjectionResult { Ok, AlreadyInjected, WrongBitness, OpenFailed, Blocked, Failed }

internal interface IInjectionService
{
    void EnsureReady();
    void WriteActiveCursor(CursorImage image);
    InjectionResult Inject(uint pid);
    bool InjectWindowTree(IntPtr hwnd, uint mainPid);
}

// ---- foreground detection (SRP) ----
internal interface IForegroundMonitor : IDisposable
{
    event Action<ProcessTarget>? TargetChanged;
    void Start();
    void Stop();
    ProcessTarget Current();
}

// ---- persistence / OS integration ----
internal interface ISettingsStore
{
    Config Load();
    void Save(Config config);
}

internal interface IAutoStartService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

internal interface IUpdateService
{
    Version Current { get; }
    Task CheckAsync(bool manual);
}

// ---- cursor application strategy (OCP) ----
/// <summary>Applies (or clears) our cursor for the active app using one concrete technique.</summary>
internal interface ICursorStrategy
{
    StrategyKind Kind { get; }
    void Apply(CursorImage image);
    void Clear();
}

/// <summary>Resolves which strategy an app should use (learned + rule based).</summary>
internal interface IStrategyResolver
{
    /// <summary>Return the known/decided strategy kind for a target, or null if still being decided
    /// asynchronously (treat as System until resolved).</summary>
    StrategyKind? Resolve(ProcessTarget target, Action onResolved);
}

/// <summary>An open set of rules that classify a process to a fixed strategy without injection
/// (self, shell/system, anti-cheat). Add rules without touching the resolver (OCP).</summary>
internal interface IProcessRule
{
    bool TryClassify(string exe, out StrategyKind kind);
}
