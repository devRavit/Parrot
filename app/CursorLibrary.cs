using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace Parrot;

internal enum CursorKind { Procedural, Image }

internal sealed class CursorDef
{
    public required string Name { get; init; }
    public CursorKind Kind { get; init; }
    public int ProceduralId { get; init; }          // 1..9 for procedural
    public byte[]? ImageData { get; init; }          // PNG bytes for image designs
    public bool ImageIsArrow { get; init; }          // hotspot at tip vs center
    public bool Tintable => Kind == CursorKind.Procedural;
}

/// <summary>The catalog of available cursor designs: built-in procedural ones plus any bundled
/// or user-supplied PNG images. Renders a chosen design to a straight-alpha bitmap + hotspot.</summary>
internal static class CursorLibrary
{
    public static string UserDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Parrot", "cursors");

    private static List<CursorDef>? _all;
    public static IReadOnlyList<CursorDef> All => _all ??= BuildAll();

    private static List<CursorDef> BuildAll()
    {
        var list = new List<CursorDef>();

        // 1) built-in procedural designs
        for (int i = 1; i <= CursorArt.DesignCount; i++)
            list.Add(new CursorDef { Name = CursorArt.DesignNames[i - 1], Kind = CursorKind.Procedural, ProceduralId = i });

        // 2) bundled PNGs embedded as resources (cursors\*.png)
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (!res.Contains(".cursors.", StringComparison.OrdinalIgnoreCase) ||
                    !res.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                using var s = asm.GetManifestResourceStream(res);
                if (s == null) continue;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                string fname = res[(res.IndexOf(".cursors.", StringComparison.OrdinalIgnoreCase) + 9)..];
                string name = Path.GetFileNameWithoutExtension(fname);
                list.Add(new CursorDef
                {
                    Name = Pretty(name),
                    Kind = CursorKind.Image,
                    ImageData = ms.ToArray(),
                    ImageIsArrow = IsPointerName(name)
                });
            }
        }
        catch { }

        // 3) user-supplied PNGs
        try
        {
            if (Directory.Exists(UserDir))
                foreach (var f in Directory.GetFiles(UserDir, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    list.Add(new CursorDef
                    {
                        Name = Pretty(name),
                        Kind = CursorKind.Image,
                        ImageData = File.ReadAllBytes(f),
                        ImageIsArrow = IsPointerName(name)
                    });
                }
        }
        catch { }

        return list;
    }

    public static void Refresh() => _all = null;

    private static string Pretty(string raw) =>
        raw.Replace('_', ' ').Replace('-', ' ').Trim();

    public static CursorDef Find(string name) =>
        All.FirstOrDefault(d => d.Name == name) ?? All[0];

    /// <summary>Render a design to a straight-alpha bitmap and hotspot for the given size/color.</summary>
    public static (Bitmap bmp, int hotX, int hotY) Render(CursorDef def, int sizeIdx, int colorIdx)
    {
        if (def.Kind == CursorKind.Procedural)
            return CursorArt.Render(def.ProceduralId, sizeIdx, colorIdx);

        int target = CursorArt.SizePx[Math.Clamp(sizeIdx, 0, CursorArt.SizeCount - 1)];
        using var src = LoadPng(def.ImageData!);
        // scale preserving aspect into a target x target box
        float scale = Math.Min((float)target / src.Width, (float)target / src.Height);
        int dw = Math.Max(1, (int)(src.Width * scale));
        int dh = Math.Max(1, (int)(src.Height * scale));
        var bmp = new Bitmap(dw, dh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(src, new Rectangle(0, 0, dw, dh));
        }
        if (colorIdx >= 0) Tint(bmp, CursorArt.Colors[Math.Clamp(colorIdx, 0, CursorArt.ColorCount - 1)].c);
        var (hotX, hotY) = ComputeHotspot(bmp, def.ImageIsArrow);
        return (bmp, hotX, hotY);
    }

    public static bool IsPointerName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("arrow") || n.Contains("pointer") || n.Contains("point") || n.Contains("gauntlet");
    }

    /// <summary>Compute a sensible hotspot from image content.
    /// Pointer/finger shapes anchor at the tip (midpoint of the topmost opaque row, which is the
    /// narrow point for both up and up-left cursors). Symmetric shapes anchor at their centroid.</summary>
    private static unsafe (int x, int y) ComputeHotspot(Bitmap bmp, bool pointerLike)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            byte* baseP = (byte*)data.Scan0;
            int minY = -1, rowMinX = int.MaxValue, rowMaxX = int.MinValue;
            long sumX = 0, sumY = 0, count = 0;
            for (int y = 0; y < data.Height; y++)
            {
                byte* row = baseP + y * data.Stride;
                for (int x = 0; x < data.Width; x++)
                {
                    if (row[x * 4 + 3] <= 50) continue; // alpha threshold
                    sumX += x; sumY += y; count++;
                    if (minY < 0) minY = y;
                    if (y == minY) { if (x < rowMinX) rowMinX = x; if (x > rowMaxX) rowMaxX = x; }
                }
            }
            if (count == 0) return (bmp.Width / 2, bmp.Height / 2);
            if (pointerLike && minY >= 0)
                return ((rowMinX + rowMaxX) / 2, minY);      // tip = midpoint of topmost opaque row
            return ((int)(sumX / count), (int)(sumY / count)); // centroid
        }
        finally { bmp.UnlockBits(data); }
    }

    /// <summary>A small preview thumbnail for the gallery (fits within box).</summary>
    public static Bitmap Preview(CursorDef def, int box, int colorIdx)
    {
        var (full, _, _) = Render(def, 3 /* Large */, colorIdx);
        try
        {
            float scale = Math.Min((float)box / full.Width, (float)box / full.Height);
            int dw = Math.Max(1, (int)(full.Width * scale));
            int dh = Math.Max(1, (int)(full.Height * scale));
            var thumb = new Bitmap(box, box, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(thumb);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);
            g.DrawImage(full, new Rectangle((box - dw) / 2, (box - dh) / 2, dw, dh));
            return thumb;
        }
        finally { full.Dispose(); }
    }

    /// <summary>Multiply-tint an image (keeps dark outlines, colors the light fill). BGRA memory order.</summary>
    private static unsafe void Tint(Bitmap bmp, Color c)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var d = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            byte* p = (byte*)d.Scan0;
            int tot = d.Height * d.Stride;
            for (int i = 0; i < tot; i += 4)
            {
                if (p[i + 3] == 0) continue;
                p[i] = (byte)(p[i] * c.B / 255);       // B
                p[i + 1] = (byte)(p[i + 1] * c.G / 255); // G
                p[i + 2] = (byte)(p[i + 2] * c.R / 255); // R
            }
        }
        finally { bmp.UnlockBits(d); }
    }

    private static Bitmap LoadPng(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var tmp = new Bitmap(ms);
        return new Bitmap(tmp); // detach from stream
    }
}
