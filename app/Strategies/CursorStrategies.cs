namespace Parrot;

/// <summary>Normal apps: replace the Windows system cursor.</summary>
internal sealed class SystemCursorStrategy : ICursorStrategy
{
    private readonly ISystemCursorService _sys;
    private readonly Config _cfg;
    public SystemCursorStrategy(ISystemCursorService sys, Config cfg) { _sys = sys; _cfg = cfg; }
    public StrategyKind Kind => StrategyKind.System;
    public void Apply(CursorImage image) => _sys.Apply(image, _cfg.ReplaceAllTypes);
    public void Clear() { /* system cursor stays as the global baseline */ }
}

/// <summary>Apps that allow injection (MuMu, LDPlayer, most windowed apps): the injected hook
/// replaces their cursor; we also keep the system cursor as a baseline.</summary>
internal sealed class InjectionStrategy : ICursorStrategy
{
    private readonly IInjectionService _inj;
    private readonly ISystemCursorService _sys;
    private readonly Config _cfg;
    public InjectionStrategy(IInjectionService inj, ISystemCursorService sys, Config cfg) { _inj = inj; _sys = sys; _cfg = cfg; }
    public StrategyKind Kind => StrategyKind.Injection;
    public void Apply(CursorImage image)
    {
        _inj.WriteActiveCursor(image);          // injected hook live-reloads this
        _sys.Apply(image, _cfg.ReplaceAllTypes); // baseline for any non-hooked windows
    }
    public void Clear() { /* injected hook persists in the target; system baseline stays */ }
}

/// <summary>Apps that block injection: hide the OS cursor and draw a topmost overlay that follows
/// the mouse.</summary>
internal sealed class OverlayStrategy : ICursorStrategy
{
    private readonly IOverlayService _overlay;
    private readonly ISystemCursorService _sys;
    public OverlayStrategy(IOverlayService overlay, ISystemCursorService sys) { _overlay = overlay; _sys = sys; }
    public StrategyKind Kind => StrategyKind.Overlay;
    public void Apply(CursorImage image) { _sys.Blank(); _overlay.Show(image); }
    public void Clear() { _overlay.Hide(); _sys.Restore(); }
}

/// <summary>Resolves ICursorStrategy instances by kind (simple factory).</summary>
internal sealed class StrategyFactory
{
    private readonly IReadOnlyDictionary<StrategyKind, ICursorStrategy> _map;
    public StrategyFactory(SystemCursorStrategy system, InjectionStrategy injection, OverlayStrategy overlay)
    {
        _map = new Dictionary<StrategyKind, ICursorStrategy>
        {
            [StrategyKind.System] = system,
            [StrategyKind.Injection] = injection,
            [StrategyKind.Overlay] = overlay,
        };
    }
    public ICursorStrategy Get(StrategyKind kind) => _map[kind];
}
