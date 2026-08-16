using System.Drawing;
using System.Drawing.Drawing2D;

namespace Parrot;

/// <summary>Role-specific cursor shapes (resize / move / text / cross / no) so EVERY mouse-event
/// cursor state is rendered at a consistent size &amp; color — eliminates the size-jump flicker at
/// window resize borders.</summary>
internal static partial class CursorArt
{
    private static (Bitmap bmp, int hotX, int hotY) NewCanvas(int sizeIdx, out Graphics g, out int s, out Color col, out int pad)
    {
        s = SizePx[Math.Clamp(sizeIdx, 0, SizeCount - 1)];
        col = Colors[Math.Clamp(0, 0, Colors.Length - 1)].c; // overwritten by caller-provided color
        pad = Math.Max(4, s / 20);   // small margin so total size ~= pointer size (avoids size-jump)
        int w = s + pad * 2, h = s + pad * 2;
        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        return (bmp, w / 2, h / 2);
    }

    /// <summary>Double-headed resize arrow at the given angle (0=horizontal ↔, 90=vertical ↕,
    /// 45=NESW ⤢, 135=NWSE ⤡). Hotspot = center.</summary>
    public static (Bitmap, int, int) RenderResize(int sizeIdx, int colorIdx, double angleDeg)
    {
        var (bmp, hx, hy) = NewCanvas(sizeIdx, out var g, out int s, out _, out _);
        using (g)
        {
            Color col = Colors[Math.Clamp(colorIdx < 0 ? 5 : colorIdx, 0, Colors.Length - 1)].c;
            float cx = bmp.Width / 2f, cy = bmp.Height / 2f;
            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)angleDeg);
            DrawDoubleArrowH(g, s * 0.44f, s * 0.22f, s * 0.2f, Math.Max(4f, s / 9f), col);
        }
        return (bmp, hx, hy);
    }

    /// <summary>4-way move arrows (SizeAll ✥). Hotspot = center.</summary>
    public static (Bitmap, int, int) RenderSizeAll(int sizeIdx, int colorIdx)
    {
        var (bmp, hx, hy) = NewCanvas(sizeIdx, out var g, out int s, out _, out _);
        using (g)
        {
            Color col = Colors[Math.Clamp(colorIdx < 0 ? 5 : colorIdx, 0, Colors.Length - 1)].c;
            float cx = bmp.Width / 2f, cy = bmp.Height / 2f;
            for (int a = 0; a < 4; a++)
            {
                g.ResetTransform();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(a * 90f);
                DrawArrowHead(g, s * 0.45f, s * 0.18f, Math.Max(4f, s / 9f), col);
            }
        }
        return (bmp, hx, hy);
    }

    /// <summary>Text I-beam. Hotspot = center.</summary>
    public static (Bitmap, int, int) RenderIBeam(int sizeIdx, int colorIdx)
    {
        var (bmp, hx, hy) = NewCanvas(sizeIdx, out var g, out int s, out _, out _);
        using (g)
        {
            Color col = Colors[Math.Clamp(colorIdx < 0 ? 5 : colorIdx, 0, Colors.Length - 1)].c;
            float cx = bmp.Width / 2f, cy = bmp.Height / 2f, half = s * 0.42f, serif = s * 0.12f;
            float t = Math.Max(3f, s / 16f);
            void Bars(Pen p)
            {
                g.DrawLine(p, cx, cy - half, cx, cy + half);
                g.DrawLine(p, cx - serif, cy - half, cx + serif, cy - half);
                g.DrawLine(p, cx - serif, cy + half, cx + serif, cy + half);
            }
            using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 22f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Bars(dark);
            using (var pen = new Pen(col, t) { StartCap = LineCap.Round, EndCap = LineCap.Round }) Bars(pen);
        }
        return (bmp, hx, hy);
    }

    /// <summary>Precision crosshair (thin, full-length, no gap). Hotspot = center.</summary>
    public static (Bitmap, int, int) RenderCrossHair(int sizeIdx, int colorIdx)
    {
        var (bmp, hx, hy) = NewCanvas(sizeIdx, out var g, out int s, out _, out _);
        using (g)
        {
            Color col = Colors[Math.Clamp(colorIdx < 0 ? 5 : colorIdx, 0, Colors.Length - 1)].c;
            float cx = bmp.Width / 2f, cy = bmp.Height / 2f, half = s * 0.48f, t = Math.Max(2.5f, s / 22f);
            void Lines(Pen p) { g.DrawLine(p, cx, cy - half, cx, cy + half); g.DrawLine(p, cx - half, cy, cx + half, cy); }
            using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 26f))) Lines(dark);
            using (var pen = new Pen(col, t)) Lines(pen);
        }
        return (bmp, hx, hy);
    }

    /// <summary>Unavailable / "no" cursor (circle with slash). Hotspot = center.</summary>
    public static (Bitmap, int, int) RenderNo(int sizeIdx, int colorIdx)
    {
        var (bmp, hx, hy) = NewCanvas(sizeIdx, out var g, out int s, out _, out _);
        using (g)
        {
            Color col = Color.FromArgb(240, 60, 60);
            float cx = bmp.Width / 2f, cy = bmp.Height / 2f, r = s * 0.4f, t = Math.Max(4f, s / 10f);
            var rr = new RectangleF(cx - r, cy - r, r * 2, r * 2);
            using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 22f))) g.DrawEllipse(dark, rr);
            using (var pen = new Pen(col, t)) g.DrawEllipse(pen, rr);
            float d = r * 0.7071f;
            using (var dark = new Pen(DarkEdge, t + Math.Max(2f, s / 22f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(dark, cx - d, cy - d, cx + d, cy + d);
            using (var pen = new Pen(col, t) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
        }
        return (bmp, hx, hy);
    }

    // draw a horizontal double-headed arrow centered at the current transform origin
    private static void DrawDoubleArrowH(Graphics g, float half, float headLen, float headW, float thick, Color col)
    {
        using var path = new GraphicsPath();
        path.AddPolygon(new[] { new PointF(-half, 0), new PointF(-half + headLen, -headW), new PointF(-half + headLen, headW) });
        path.AddPolygon(new[] { new PointF(half, 0), new PointF(half - headLen, -headW), new PointF(half - headLen, headW) });
        using (var dark = new Pen(DarkEdge, thick + 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(dark, -half + headLen * 0.6f, 0, half - headLen * 0.6f, 0);
        using (var shaft = new Pen(col, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(shaft, -half + headLen * 0.6f, 0, half - headLen * 0.6f, 0);
        using (var dpen = new Pen(DarkEdge, Math.Max(2f, thick / 2)) { LineJoin = LineJoin.Round })
        using (var br = new SolidBrush(col))
        { g.FillPath(br, path); g.DrawPath(dpen, path); }
    }

    // one arrowhead pointing up from origin (used 4x for SizeAll)
    private static void DrawArrowHead(Graphics g, float dist, float headW, float thick, Color col)
    {
        using var path = new GraphicsPath();
        path.AddPolygon(new[] { new PointF(0, -dist), new PointF(-headW, -dist + headW * 1.4f), new PointF(headW, -dist + headW * 1.4f) });
        using (var dark = new Pen(DarkEdge, thick + 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(dark, 0, 0, 0, -dist + headW);
        using (var shaft = new Pen(col, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(shaft, 0, 0, 0, -dist + headW);
        using (var dpen = new Pen(DarkEdge, Math.Max(2f, thick / 2)) { LineJoin = LineJoin.Round })
        using (var br = new SolidBrush(col))
        { g.FillPath(br, path); g.DrawPath(dpen, path); }
    }
}
