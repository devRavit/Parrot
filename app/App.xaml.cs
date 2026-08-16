using System.Drawing;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace Parrot;

public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? _mtx;

    private CursorController _controller = null!;
    private IUpdateService _updater = null!;
    private ICursorProvider _provider = null!;
    private IAutoStartService _autoStart = null!;
    private WinForms.NotifyIcon? _tray;
    private MainWindow? _win;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (HandleCli(e.Args)) { Shutdown(); return; }

        _mtx = new System.Threading.Mutex(true, "Parrot_SingleInstance", out bool isNew);
        if (!isNew) { Shutdown(); return; }
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // ---- composition root (poor-man's DI) ----
        var store = new IniSettingsStore();
        var cfg = store.Load();
        _provider = new CursorProvider();
        var sys = new SystemCursorService();
        var overlay = new OverlayService();
        var inj = new InjectionService();
        _autoStart = new AutoStartService();
        var rules = new IProcessRule[] { new SelfRule(), new SystemShellRule(), new AntiCheatRule() };
        var resolver = new StrategyResolver(rules, inj, _provider, cfg, store);
        var factory = new StrategyFactory(
            new SystemCursorStrategy(sys, cfg),
            new InjectionStrategy(inj, sys, cfg),
            new OverlayStrategy(overlay, sys));
        var monitor = new ForegroundMonitor();
        _controller = new CursorController(cfg, _provider, resolver, factory, monitor, sys, overlay, inj, store);
        _updater = new GitHubUpdateService(ExitForUpdate);

        _tray = BuildTray();
        _controller.Start();

        _win = new MainWindow(_controller, _provider, _autoStart);
        _win.Show();

        _ = _updater.CheckAsync(manual: false);   // background auto-update
    }

    private WinForms.NotifyIcon BuildTray()
    {
        var tray = new WinForms.NotifyIcon { Icon = BuildTrayIcon(), Visible = true, Text = "Parrot" };
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("열기", null, (_, _) => ShowWindow());
        menu.Items.Add("적용 / 끄기", null, (_, _) => _controller.Toggle());
        menu.Items.Add("업데이트 확인", null, (_, _) => { _ = _updater.CheckAsync(manual: true); });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        var ver = menu.Items.Add($"Parrot  v{_updater.Current.ToString(3)}"); ver.Enabled = false;
        menu.Items.Add("종료", null, (_, _) => ExitApp());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowWindow();
        return tray;
    }

    private void ShowWindow()
    {
        _win ??= new MainWindow(_controller, _provider, _autoStart);
        _win.Show();
        _win.WindowState = WindowState.Normal;
        _win.Activate();
        _win.Topmost = true; _win.Topmost = false;
    }

    internal void ExitApp()
    {
        try { _controller.Shutdown(); } catch { }
        try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
        Shutdown();
    }

    private void ExitForUpdate() => ExitApp();

    /// <summary>CLI: Parrot.exe --inject &lt;pid&gt; (utility for manual/scripted injection).</summary>
    private static bool HandleCli(string[] args)
    {
        int ix = Array.FindIndex(args, s => s.Equals("--inject", StringComparison.OrdinalIgnoreCase));
        if (ix < 0 || ix + 1 >= args.Length || !uint.TryParse(args[ix + 1], out uint pid)) return false;
        try
        {
            var store = new IniSettingsStore();
            var cfg = store.Load();
            var provider = new CursorProvider();
            var inj = new InjectionService();
            inj.EnsureReady();
            using (var img = provider.Render(new CursorSpec(cfg.DesignName, cfg.Size, cfg.Color, cfg.ReplaceAllTypes)))
                inj.WriteActiveCursor(img);
            inj.Inject(pid);
        }
        catch { }
        return true;
    }

    private static Icon BuildTrayIcon()
    {
        var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var pts = new PointF[] { new(5, 3), new(5, 27), new(12, 21), new(17, 31), new(21, 29), new(16, 19), new(25, 19) };
            using var br = new SolidBrush(Color.FromArgb(61, 220, 132));
            using var pen = new Pen(Color.FromArgb(18, 18, 20), 2f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            g.FillPolygon(br, pts);
            g.DrawPolygon(pen, pts);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
