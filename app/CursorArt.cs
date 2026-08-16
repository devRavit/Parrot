using System.Drawing;
using System.Drawing.Drawing2D;

namespace Parrot;

/// <summary>High-quality procedural cursor designs (gradient fill, soft shadow, dual outline).
/// Returns straight-alpha bitmaps suitable for CreateIconIndirect.</summary>
internal static class CursorArt
{
    public static readonly string[] DesignNames =
        { "Arrow", "Crosshair", "Ring", "Dot", "Diamond", "Plus", "Target", "Bracket", "Bold Arrow" };
    public const int DesignCount = 9;

    public static readonly string[] SizeNames = { "Tiny", "Small", "Medium", "Large", "Huge" };
    public static readonly int[] SizePx = { 32, 48, 72, 104, 148 };
    public const int SizeCount = 5;

    public static readonly (string name, Color c)[] Colors =
    {
        ("Lime",    Color.FromArgb( 96, 230,  60)),
        ("Red",     Color.FromArgb(240,  60,  60)),
        ("Gold",    Color.FromArgb(255, 200,  40)),
        ("Cyan",    Color.FromArgb( 40, 210, 245)),
        ("Magenta", Color.FromArgb(240,  70, 210)),
        ("White",   Color.FromArgb(245, 245, 248)),
        ("Orange",  Color.FromArgb(255, 140,  30)),
        ("Blue",    Color.FromArgb( 70, 130, 255)),
    };
    public static int ColorCount => Colors.Length;

    public static (Bitmap bmp, int hotX, int hotY) Render(int design, int sizeIdx, int colorIdx)
    {
        int s = SizePx[Math.Clamp(sizeIdx, 0, SizeCount - 1)];
        // colorIdx < 0 means "original" -> procedural designs fall back to a clean white
        Color col = Colors[colorIdx < 0 ? 5 : Math.Clamp(colorIdx, 0, Colors.Length - 1)].c;
        int pad = Math.Max(8, s / 8);
        int w = s + pad * 2, h = s + pad * 2;

        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        int hotX, hotY;
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            switch (design)
            {
                case 1: Arrow(g, pad, s, col, false, out hotX, out hotY); break;
                case 2: Crosshair(g, w, h, s, col, out hotX, out hotY); break;
                case 3: Ring(g, w, h, s, col, out hotX, out hotY); break;
                case 4: Dot(g, w, h, s, col, out hotX, out hotY); break;
                case 5: Diamond(g, w, h, s, col, out hotX, out hotY); break;
                case 6: Plus(g, w, h, s, col, out hotX, out hotY); break;
                case 7: Target(g, w, h, s, col, out hotX, out hotY); break;
                case 8: Bracket(g, w, h, s, col, out hotX, out hotY); break;
                case 9: Arrow(g, pad, s, col, true, out hotX, out hotY); break;
                default: goto case 1;
            }
        }
        return (bmp, hotX, hotY);
    }

    // ---- shared helpers ----
    private static readonly Color Shadow = Color.FromArgb(90, 0, 0, 0);
    private static readonly Color DarkEdge = Color.FromArgb(235, 12, 12, 14);

    private static LinearGradientBrush Grad(RectangleF r, Color c)
    {
        Color top = Lighten(c, 0.35f);
        Color bot = Darken(c, 0.12f);
        return new LinearGradientBrush(r, top, bot, LinearGradientMode.Vertical);
    }
    private static Color Lighten(Color c, float f) =>
        Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * f), (int)(c.G + (255 - c.G) * f), (int)(c.B + (255 - c.B) * f));
    private static Color Darken(Color c, float f) =>
        Color.FromArgb(c.A, (int)(c.R * (1 - f)), (int)(c.G * (1 - f)), (int)(c.B * (1 - f)));

    private static void ShadowPath(Graphics g, GraphicsPath p, float blur)
    {
        using var pen = new Pen(Shadow, blur) { LineJoin = LineJoin.Round };
        var m = new Matrix(); m.Translate(blur * 0.4f, blur * 0.5f);
        var clone = (GraphicsPath)p.Clone(); clone.Transform(m);
        using var br = new SolidBrush(Shadow);
        g.DrawPath(pen, clone);
        g.FillPath(br, clone);
        clone.Dispose();
    }

    private static void Arrow(Graphics g, int pad, int s, Color col, bool bold, out int hotX, out int hotY)
    {
        float u = s / 100f;
        PointF[] pts = bold
            ? new PointF[] { new(0,0), new(0,82), new(23,61), new(39,96), new(56,88), new(40,55), new(66,55) }
            : new PointF[] { new(0,0), new(0,74), new(20,56), new(33,88), new(46,82), new(34,51), new(58,51) };
        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(pad + pts[i].X * u, pad + pts[i].Y * u);

        using var path = new GraphicsPath();
        path.AddPolygon(pts);
        ShadowPath(g, path, Math.Max(3f, s / 20f));

        var bounds = path.GetBounds();
        using (var br = Grad(bounds, col)) g.FillPath(br, path);
        using (var dark = new Pen(DarkEdge, Math.Max(2f, s / 26f)) { LineJoin = LineJoin.Round })
            g.DrawPath(dark, path);
        using (var lite = new Pen(Color.FromArgb(150, 255, 255, 255), Math.Max(1f, s / 60f)) { LineJoin = LineJoin.Round })
            g.DrawPath(lite, path);
        hotX = pad; hotY = pad;
    }

    private static void Crosshair(Graphics g, int w, int h, int s, Color col, out int hotX, out int hotY)
    {
        float cx = w / 2f, cy = h / 2f, half = s * 0.5f, gap = s * 0.13f, t = Math.Max(3f, s / 15f);
        void Arms(Pen p)
        {
            g.DrawLine(p, cx, cy - half, cx, cy - gap);
            g.DrawLine(p, cx, cy + gap, cx, cy + half);
            g.DrawLine(p, cx - half, cy, cx - gap, cy);
            g.DrawLine(p, cx + gap, cy, cx + half, cy);
        }
        using (var sh = new Pen(Shadow, t + Math.Max(3f, s / 16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Arms(sh);
        using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 24f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Arms(dark);
        using (var pen = new Pen(col, t) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Arms(pen);
        hotX = (int)cx; hotY = (int)cy;
    }

    private static void Ring(Graphics g, int w, int h, int s, Color col, out int hotX, out int hotY)
    {
        float cx = w / 2f, cy = h / 2f, r = s * 0.42f, t = Math.Max(3f, s / 11f);
        var rr = new RectangleF(cx - r, cy - r, r * 2, r * 2);
        using (var sh = new Pen(Shadow, t + Math.Max(3f, s / 16f))) g.DrawEllipse(sh, rr);
        using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 24f))) g.DrawEllipse(dark, rr);
        using (var pen = new Pen(col, t)) g.DrawEllipse(pen, rr);
        float dr = Math.Max(2f, s / 24f);
        using (var db = new SolidBrush(col)) g.FillEllipse(db, cx - dr, cy - dr, dr * 2, dr * 2);
        hotX = (int)cx; hotY = (int)cy;
    }

    private static void Dot(Graphics g, int w, int h, int s, Color col, out int hotX, out int hotY)
    {
        float cx = w / 2f, cy = h / 2f, r = s * 0.32f;
        using (var db = new SolidBrush(Shadow)) g.FillEllipse(db, cx - r + s * 0.02f, cy - r + s * 0.03f, r * 2, r * 2);
        var rr = new RectangleF(cx - r, cy - r, r * 2, r * 2);
        using (var br = new PathGradientBrushSafe(rr, Lighten(col, 0.4f), Darken(col, 0.15f))) br.Fill(g, rr);
        using (var dark = new Pen(DarkEdge, Math.Max(2f, s / 26f))) g.DrawEllipse(dark, rr);
        hotX = (int)cx; hotY = (int)cy;
    }

    private static void Diamond(Graphics g, int w, int h, int s, Color col, out int hotX, out int hotY)
    {
        float cx = w / 2f, cy = h / 2f, r = s * 0.46f;
        PointF[] pts = { new(cx, cy - r), new(cx + r, cy), new(cx, cy + r), new(cx - r, cy) };
        using var path = new GraphicsPath(); path.AddPolygon(pts);
        ShadowPath(g, path, Math.Max(3f, s / 18f));
        using (var br = Grad(path.GetBounds(), col)) g.FillPath(br, path);
        using (var dark = new Pen(DarkEdge, Math.Max(2f, s / 24f)) { LineJoin = LineJoin.Round }) g.DrawPath(dark, path);
        hotX = (int)cx; hotY = (int)cy;
    }

    private static void Plus(Graphics g, int w, int h, int s, Color col, out int hotX, out int hotY)
    {
        float cx = w / 2f, cy = h / 2f, half = s * 0.46f, t = Math.Max(5f, s / 6.5f);
        void Arms(Pen p) { g.DrawLine(p, cx, cy - half, cx, cy + half); g.DrawLine(p, cx - half, cy, cx + half, cy); }
        using (var sh = new Pen(Shadow, t + Math.Max(3f, s / 16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Arms(sh);
        using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 22f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Arms(dark);
        using (var pen = new Pen(col, t) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Arms(pen);
        hotX = (int)cx; hotY = (int)cy;
    }

    private static void Target(Graphics g, int w, int h, int s, Color col, out int hotX, out int hotY)
    {
        float cx = w / 2f, cy = h / 2f, t = Math.Max(3f, s / 15f);
        foreach (var r in new[] { s * 0.44f, s * 0.26f })
        {
            var rr = new RectangleF(cx - r, cy - r, r * 2, r * 2);
            using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 24f))) g.DrawEllipse(dark, rr);
            using (var pen = new Pen(col, t)) g.DrawEllipse(pen, rr);
        }
        // ticks
        float half = s * 0.5f, inr = s * 0.44f;
        using (var pen = new Pen(col, t))
        {
            g.DrawLine(pen, cx, cy - half, cx, cy - inr * 0.7f);
            g.DrawLine(pen, cx, cy + inr * 0.7f, cx, cy + half);
            g.DrawLine(pen, cx - half, cy, cx - inr * 0.7f, cy);
            g.DrawLine(pen, cx + inr * 0.7f, cy, cx + half, cy);
        }
        float dr = Math.Max(2.5f, s / 22f);
        using (var db = new SolidBrush(col)) g.FillEllipse(db, cx - dr, cy - dr, dr * 2, dr * 2);
        hotX = (int)cx; hotY = (int)cy;
    }

    private static void Bracket(Graphics g, int w, int h, int s, Color col, out int hotX, out int hotY)
    {
        float cx = w / 2f, cy = h / 2f, r = s * 0.44f, len = s * 0.22f, t = Math.Max(3f, s / 12f);
        (float x, float y)[] corners = { (cx - r, cy - r), (cx + r, cy - r), (cx + r, cy + r), (cx - r, cy + r) };
        void DrawBrackets(Pen p)
        {
            // TL
            g.DrawLine(p, cx - r, cy - r, cx - r + len, cy - r); g.DrawLine(p, cx - r, cy - r, cx - r, cy - r + len);
            // TR
            g.DrawLine(p, cx + r, cy - r, cx + r - len, cy - r); g.DrawLine(p, cx + r, cy - r, cx + r, cy - r + len);
            // BR
            g.DrawLine(p, cx + r, cy + r, cx + r - len, cy + r); g.DrawLine(p, cx + r, cy + r, cx + r, cy + r - len);
            // BL
            g.DrawLine(p, cx - r, cy + r, cx - r + len, cy + r); g.DrawLine(p, cx - r, cy + r, cx - r, cy + r - len);
        }
        using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 24f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) DrawBrackets(dark);
        using (var pen = new Pen(col, t) { StartCap = LineCap.Round, EndCap = LineCap.Round }) DrawBrackets(pen);
        float dr = Math.Max(2f, s / 26f);
        using (var db = new SolidBrush(col)) g.FillEllipse(db, cx - dr, cy - dr, dr * 2, dr * 2);
        hotX = (int)cx; hotY = (int)cy;
    }

    // small radial-fill helper for the Dot
    private sealed class PathGradientBrushSafe : IDisposable
    {
        private readonly Color _center, _edge;
        public PathGradientBrushSafe(RectangleF r, Color center, Color edge) { _center = center; _edge = edge; }
        public void Fill(Graphics g, RectangleF r)
        {
            using var path = new GraphicsPath(); path.AddEllipse(r);
            using var b = new PathGradientBrush(path)
            { CenterColor = _center, SurroundColors = new[] { _edge }, CenterPoint = new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.35f) };
            g.FillPath(b, path);
        }
        public void Dispose() { }
    }
}
