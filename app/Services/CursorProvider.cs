using System.Drawing;
using static Parrot.Native;

namespace Parrot;

/// <summary>Adapts the cursor catalog/renderer to the ICursorProvider abstraction, including a full
/// per-mouse-state cursor scheme.</summary>
internal sealed class CursorProvider : ICursorProvider
{
    public IReadOnlyList<CursorDef> Designs => CursorLibrary.All;

    public CursorImage Render(CursorSpec spec)
    {
        var def = CursorLibrary.Find(spec.Design);
        var (bmp, hx, hy) = CursorLibrary.Render(def, spec.Size, spec.Color);
        return new CursorImage(bmp, hx, hy);
    }

    public IReadOnlyDictionary<uint, CursorImage> RenderScheme(CursorSpec spec)
    {
        var def = CursorLibrary.Find(spec.Design);
        var d = new Dictionary<uint, CursorImage>();

        // Pointer-like states use the chosen design.
        foreach (var id in new[] { OCR_NORMAL, OCR_HAND, OCR_APPSTARTING, OCR_HELP, OCR_UP })
        {
            var (b, hx, hy) = CursorLibrary.Render(def, spec.Size, spec.Color);
            d[id] = new CursorImage(b, hx, hy);
        }

        // Resize / move / precision states -> matching custom shapes, same size & color (no flicker).
        d[OCR_SIZEWE] = Wrap(CursorArt.RenderResize(spec.Size, spec.Color, 0));
        d[OCR_SIZENS] = Wrap(CursorArt.RenderResize(spec.Size, spec.Color, 90));
        d[OCR_SIZENWSE] = Wrap(CursorArt.RenderResize(spec.Size, spec.Color, 45));   // ↘↖
        d[OCR_SIZENESW] = Wrap(CursorArt.RenderResize(spec.Size, spec.Color, 135));  // ↗↙
        d[OCR_SIZEALL] = Wrap(CursorArt.RenderSizeAll(spec.Size, spec.Color));
        d[OCR_CROSS] = Wrap(CursorArt.RenderCrossHair(spec.Size, spec.Color));
        d[OCR_NO] = Wrap(CursorArt.RenderNo(spec.Size, spec.Color));

        // Text I-beam only when "replace all types" is on (keeps precise native I-beam otherwise).
        if (spec.ReplaceAllTypes)
            d[OCR_IBEAM] = Wrap(CursorArt.RenderIBeam(spec.Size, spec.Color));

        // OCR_WAIT / animated busy states are left native.
        return d;
    }

    private static CursorImage Wrap((Bitmap bmp, int hx, int hy) t) => new(t.bmp, t.hx, t.hy);

    public Bitmap Preview(CursorDef def, int box, int colorIndex) => CursorLibrary.Preview(def, box, colorIndex);

    public void Reload() => CursorLibrary.Refresh();
}
