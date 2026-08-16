using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32.SafeHandles;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfCursor = System.Windows.Input.Cursor;

namespace Parrot;

public partial class MainWindow : Window
{
    private readonly CursorController _controller;
    private readonly ICursorProvider _provider;
    private readonly IAutoStartService _autoStart;
    private readonly ObservableCollection<TileVM> _tiles = new();
    private bool _loading = true;

    private WpfCursor? _cNormal, _cHover, _cPress;
    private bool _pressed;

    private Config Cfg => _controller.Cfg;

    internal MainWindow(CursorController controller, ICursorProvider provider, IAutoStartService autoStart)
    {
        _controller = controller; _provider = provider; _autoStart = autoStart;
        InitializeComponent();

        BuildTiles();
        Gallery.ItemsSource = _tiles;

        SizeSlider.Value = Cfg.Size;
        SizeLabel.Text = CursorArt.SizeNames[Cfg.Size];
        ReplaceAllToggle.IsChecked = Cfg.ReplaceAllTypes;
        AutoStartToggle.IsChecked = _autoStart.IsEnabled;
        MasterToggle.IsChecked = Cfg.Enabled;

        BuildSwatches();
        SelectCurrentDesign();
        UpdateStatus();
        UpdatePreview();
        RebuildLiveCursors();

        MinBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        CloseBtn.Click += (_, _) => Hide();
        TitleBar.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        MasterToggle.Click += (_, _) => { _controller.SetEnabled(MasterToggle.IsChecked == true); UpdateStatus(); ApplyLiveCursor(); };
        Gallery.SelectionChanged += OnDesignChanged;
        SizeSlider.ValueChanged += OnSizeChanged;
        ReplaceAllToggle.Click += (_, _) => { Cfg.ReplaceAllTypes = ReplaceAllToggle.IsChecked == true; _controller.SettingsChanged(); };
        AutoStartToggle.Click += (_, _) => _autoStart.SetEnabled(AutoStartToggle.IsChecked == true);
        OpenFolderBtn.Click += (_, _) => { try { System.IO.Directory.CreateDirectory(CursorLibrary.UserDir); Process.Start("explorer.exe", CursorLibrary.UserDir); } catch { } };

        // live cursor + click/hover states over the dashboard
        PreviewMouseDown += (_, _) => { _pressed = true; ApplyLiveCursor(); };
        PreviewMouseUp += (_, _) => { _pressed = false; ApplyLiveCursor(); };
        PreviewMouseMove += (_, e) => ApplyLiveCursor(IsOverClickable(e.OriginalSource as DependencyObject));
        MouseLeave += (_, _) => { _pressed = false; };

        _controller.Changed += () => Dispatcher.Invoke(() => { MasterToggle.IsChecked = Cfg.Enabled; UpdateStatus(); ApplyLiveCursor(); });

        _loading = false;
        ApplyLiveCursor();
    }

    protected override void OnClosing(CancelEventArgs e) { e.Cancel = true; Hide(); }

    // ---------------- gallery ----------------
    private void BuildTiles()
    {
        _tiles.Clear();
        foreach (var def in _provider.Designs)
        {
            using var bmp = _provider.Preview(def, 84, Cfg.Color);
            _tiles.Add(new TileVM(def, def.Name, ImageInterop.ToSource(bmp)));
        }
    }

    private void RefreshTilePreviews()
    {
        foreach (var t in _tiles)
        {
            using var bmp = _provider.Preview(t.Def, 84, Cfg.Color);
            t.Image = ImageInterop.ToSource(bmp);
        }
    }

    // ---------------- colors ----------------
    private void BuildSwatches()
    {
        SwatchPanel.Children.Clear();
        SwatchPanel.Children.Add(MakeSwatch(-1, null, "원본"));
        for (int i = 0; i < CursorArt.ColorCount; i++)
        {
            var c = CursorArt.Colors[i].c;
            SwatchPanel.Children.Add(MakeSwatch(i, WpfColor.FromRgb(c.R, c.G, c.B), CursorArt.Colors[i].name));
        }
    }

    private Border MakeSwatch(int idx, WpfColor? fill, string tip)
    {
        var b = new Border
        {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Margin = new Thickness(4),
            Cursor = System.Windows.Input.Cursors.Hand, ToolTip = tip,
            BorderThickness = new Thickness(3),
            BorderBrush = idx == Cfg.Color ? (WpfBrush)FindResource("Text") : System.Windows.Media.Brushes.Transparent
        };
        if (fill.HasValue) b.Background = new WpfBrush(fill.Value);
        else
        {
            b.Background = new WpfBrush(WpfColor.FromRgb(120, 120, 128));
            b.Child = new TextBlock { Text = "원", FontSize = 11, Foreground = System.Windows.Media.Brushes.White, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        }
        b.MouseLeftButtonUp += (_, _) =>
        {
            Cfg.Color = idx;
            _controller.SettingsChanged();
            BuildSwatches(); RefreshTilePreviews(); UpdatePreview(); RebuildLiveCursors(); ApplyLiveCursor();
        };
        return b;
    }

    // ---------------- selection / size ----------------
    private void SelectCurrentDesign()
    {
        var t = _tiles.FirstOrDefault(x => x.Def.Name == Cfg.DesignName) ?? _tiles.FirstOrDefault();
        if (t != null) Gallery.SelectedItem = t;
    }

    private void OnDesignChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Gallery.SelectedItem is not TileVM t) return;
        Cfg.DesignName = t.Def.Name;
        _controller.SettingsChanged();
        UpdatePreview(); RebuildLiveCursors(); ApplyLiveCursor();
    }

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        Cfg.Size = (int)Math.Round(SizeSlider.Value);
        SizeLabel.Text = CursorArt.SizeNames[Cfg.Size];
        _controller.SettingsChanged();
        UpdatePreview(); RebuildLiveCursors(); ApplyLiveCursor();
    }

    private void UpdatePreview()
    {
        try
        {
            using var img = _provider.Render(new CursorSpec(Cfg.DesignName, Cfg.Size, Cfg.Color, Cfg.ReplaceAllTypes));
            PreviewImage.Source = ImageInterop.ToSource(img.Bitmap);
        }
        catch { }
    }

    private void UpdateStatus()
    {
        bool on = Cfg.Enabled;
        StatusTitle.Text = on ? "커서 통일 — 켜짐" : "커서 통일 — 꺼짐";
        StatusText.Text = on
            ? "모든 프로그램에서 같은 커서가 표시됩니다."
            : "원래 시스템 커서를 사용 중입니다. 토글을 켜세요.";
    }

    // ---------------- live cursor over the dashboard ----------------
    private void RebuildLiveCursors()
    {
        _cNormal = MakeWpfCursor(1.0f);
        _cHover = MakeWpfCursor(1.12f);
        _cPress = MakeWpfCursor(0.85f);
    }

    private void ApplyLiveCursor(bool hover = false)
    {
        if (!Cfg.Enabled) { Cursor = System.Windows.Input.Cursors.Arrow; ForceCursor = false; return; }
        ForceCursor = true;
        Cursor = _pressed ? _cPress : (hover ? _cHover : _cNormal);
    }

    private WpfCursor? MakeWpfCursor(float scale)
    {
        try
        {
            using var img = _provider.Render(new CursorSpec(Cfg.DesignName, Cfg.Size, Cfg.Color, Cfg.ReplaceAllTypes));
            Bitmap use = img.Bitmap; int ux = img.HotX, uy = img.HotY;
            Bitmap? scaled = null;
            if (Math.Abs(scale - 1f) > 0.001f)
            {
                int nw = Math.Max(1, (int)(img.Bitmap.Width * scale));
                int nh = Math.Max(1, (int)(img.Bitmap.Height * scale));
                scaled = new Bitmap(nw, nh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.Clear(System.Drawing.Color.Transparent);
                    g.DrawImage(img.Bitmap, new Rectangle(0, 0, nw, nh));
                }
                use = scaled; ux = (int)(img.HotX * scale); uy = (int)(img.HotY * scale);
            }
            IntPtr h = CursorHandles.Build(use, ux, uy);
            scaled?.Dispose();
            if (h == IntPtr.Zero) return null;
            return CursorInteropHelper.Create(new SafeCursorHandle(h));
        }
        catch { return null; }
    }

    private static bool IsOverClickable(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is System.Windows.Controls.Primitives.ButtonBase || d is ListBoxItem) return true;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return false;
    }
}

internal sealed class SafeCursorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeCursorHandle(IntPtr h) : base(true) { SetHandle(h); }
    protected override bool ReleaseHandle() => Native.DestroyCursor(handle);
}

internal sealed class TileVM : INotifyPropertyChanged
{
    public CursorDef Def { get; }
    public string Name { get; }
    private ImageSource _image;
    public ImageSource Image { get => _image; set { _image = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image))); } }
    public TileVM(CursorDef def, string name, ImageSource image) { Def = def; Name = name; _image = image; }
    public event PropertyChangedEventHandler? PropertyChanged;
}
