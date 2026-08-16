namespace Parrot;

/// <summary>Normal apps: replace the full Windows cursor scheme (all mouse-event states).</summary>
internal sealed class SystemCursorStrategy : ICursorStrategy
{
    private readonly ISystemCursorService _sys;
    private readonly ICursorProvider _provider;
    public SystemCursorStrategy(ISystemCursorService sys, ICursorProvider provider) { _sys = sys; _provider = provider; }
    public StrategyKind Kind => StrategyKind.System;
    public void Apply(CursorSpec spec) => _sys.Apply(_provider.RenderScheme(spec));
    public void Clear() { /* system scheme stays as the global baseline */ }
}

/// <summary>Apps that allow injection: the injected hook replaces their cursor; we also keep the
/// full system scheme as a baseline.</summary>
internal sealed class InjectionStrategy : ICursorStrategy
{
    private readonly IInjectionService _inj;
    private readonly ISystemCursorService _sys;
    private readonly ICursorProvider _provider;
    public InjectionStrategy(IInjectionService inj, ISystemCursorService sys, ICursorProvider provider)
    { _inj = inj; _sys = sys; _provider = provider; }
    public StrategyKind Kind => StrategyKind.Injection;
    public void Apply(CursorSpec spec)
    {
        using (var img = _provider.Render(spec)) _inj.WriteActiveCursor(img);
        _sys.Apply(_provider.RenderScheme(spec));
    }
    public void Clear() { /* injected hook persists in the target; system baseline stays */ }
}

/// <summary>Apps that block injection: hide the OS cursor and draw a topmost overlay.</summary>
internal sealed class OverlayStrategy : ICursorStrategy
{
    private readonly IOverlayService _overlay;
    private readonly ISystemCursorService _sys;
    private readonly ICursorProvider _provider;
    public OverlayStrategy(IOverlayService overlay, ISystemCursorService sys, ICursorProvider provider)
    { _overlay = overlay; _sys = sys; _provider = provider; }
    public StrategyKind Kind => StrategyKind.Overlay;
    public void Apply(CursorSpec spec)
    {
        _sys.Blank();
        using var img = _provider.Render(spec);
        _overlay.Show(img);
    }
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
