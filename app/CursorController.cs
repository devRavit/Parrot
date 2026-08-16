using System.Windows.Threading;

namespace Parrot;

/// <summary>Orchestrates cursor application: watches the active app, resolves its strategy, and
/// applies the current cursor through that strategy. Depends only on abstractions.</summary>
internal sealed class CursorController
{
    private readonly ICursorProvider _provider;
    private readonly IStrategyResolver _resolver;
    private readonly StrategyFactory _factory;
    private readonly IForegroundMonitor _monitor;
    private readonly ISystemCursorService _sys;
    private readonly IOverlayService _overlay;
    private readonly IInjectionService _inj;
    private readonly ISettingsStore _store;
    private readonly DispatcherTimer _reassert;

    private ICursorStrategy? _active;
    private ProcessTarget _target;

    public Config Cfg { get; }
    public event Action? Changed;
    public bool Enabled => Cfg.Enabled;

    public CursorController(Config cfg, ICursorProvider provider, IStrategyResolver resolver,
        StrategyFactory factory, IForegroundMonitor monitor, ISystemCursorService sys,
        IOverlayService overlay, IInjectionService inj, ISettingsStore store)
    {
        Cfg = cfg; _provider = provider; _resolver = resolver; _factory = factory;
        _monitor = monitor; _sys = sys; _overlay = overlay; _inj = inj; _store = store;

        _reassert = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _reassert.Tick += (_, _) => { if (Cfg.Enabled && _active is { Kind: not StrategyKind.Overlay }) _sys.Reassert(); };
    }

    public void Start()
    {
        _inj.EnsureReady();
        _sys.Restore();                 // self-heal any leftover override from a crash
        _monitor.TargetChanged += OnTargetChanged;
        _monitor.Start();
        if (Cfg.Enabled) { _reassert.Start(); ApplyTo(_monitor.Current()); }
        Changed?.Invoke();
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == Cfg.Enabled) return;
        Cfg.Enabled = enabled;
        _store.Save(Cfg);
        if (enabled) { _reassert.Start(); ApplyTo(_monitor.Current()); }
        else { _reassert.Stop(); ClearActive(); _sys.Restore(); }
        Changed?.Invoke();
    }

    public void Toggle() => SetEnabled(!Cfg.Enabled);

    /// <summary>Call after changing design/size/color/replaceAll in Cfg.</summary>
    public void SettingsChanged()
    {
        _store.Save(Cfg);
        if (Cfg.Enabled && _active != null) using (var img = CurrentImage()) _active.Apply(img);
    }

    public void Shutdown()
    {
        _reassert.Stop();
        _monitor.TargetChanged -= OnTargetChanged;
        _monitor.Dispose();
        _overlay.Dispose();
        _sys.Restore();
        _store.Save(Cfg);
    }

    private void OnTargetChanged(ProcessTarget target) { if (Cfg.Enabled) ApplyTo(target); }

    private void ApplyTo(ProcessTarget target)
    {
        _target = target;
        var kind = _resolver.Resolve(target, onResolved: () => { if (Cfg.Enabled) ApplyTo(_target); })
                   ?? StrategyKind.System;   // treat undecided as System until the probe finishes
        var strategy = _factory.Get(kind);
        if (!ReferenceEquals(strategy, _active)) { _active?.Clear(); _active = strategy; }
        using var img = CurrentImage();
        _active.Apply(img);
    }

    private void ClearActive() { _active?.Clear(); _active = null; }

    private CursorImage CurrentImage() =>
        _provider.Render(new CursorSpec(Cfg.DesignName, Cfg.Size, Cfg.Color, Cfg.ReplaceAllTypes));
}
