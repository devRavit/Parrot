namespace Parrot;

/// <summary>Decides the strategy for a process: learned cache → rules → (async) injection probe.
/// New strategies/rules can be added without modifying this class (OCP).</summary>
internal sealed class StrategyResolver : IStrategyResolver
{
    private readonly IReadOnlyList<IProcessRule> _rules;
    private readonly IInjectionService _inj;
    private readonly ICursorProvider _provider;
    private readonly Config _cfg;
    private readonly ISettingsStore _store;
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);

    public StrategyResolver(IEnumerable<IProcessRule> rules, IInjectionService inj,
        ICursorProvider provider, Config cfg, ISettingsStore store)
    {
        _rules = rules.ToList();
        _inj = inj; _provider = provider; _cfg = cfg; _store = store;
    }

    public StrategyKind? Resolve(ProcessTarget target, Action onResolved)
    {
        string exe = target.Exe;
        if (_cfg.AppMethod.TryGetValue(exe, out var known)) return FromString(known);

        foreach (var rule in _rules)
            if (rule.TryClassify(exe, out var k)) { Learn(exe, k); return k; }

        // Unknown app: probe injection off the UI thread; treat as System until decided.
        if (_pending.Add(exe))
        {
            IntPtr hwnd = target.Hwnd; uint pid = target.Pid;
            System.Threading.Tasks.Task.Run(() =>
            {
                using (var img = _provider.Render(new CursorSpec(_cfg.DesignName, _cfg.Size, _cfg.Color, _cfg.ReplaceAllTypes)))
                    _inj.WriteActiveCursor(img);
                bool ok = pid != 0 && _inj.InjectWindowTree(hwnd, pid);
                var kind = ok ? StrategyKind.Injection : StrategyKind.Overlay;
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    Learn(exe, kind);
                    _pending.Remove(exe);
                    onResolved();
                });
            });
        }
        return null;
    }

    private void Learn(string exe, StrategyKind kind) { _cfg.AppMethod[exe] = ToKey(kind); _store.Save(_cfg); }

    private static StrategyKind FromString(string s) => s switch
    {
        "inject" => StrategyKind.Injection,
        "overlay" => StrategyKind.Overlay,
        _ => StrategyKind.System
    };
    private static string ToKey(StrategyKind k) => k switch
    {
        StrategyKind.Injection => "inject",
        StrategyKind.Overlay => "overlay",
        _ => "system"
    };
}
