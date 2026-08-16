using System.Drawing;

namespace Parrot;

/// <summary>Adapts the cursor catalog/renderer to the ICursorProvider abstraction.</summary>
internal sealed class CursorProvider : ICursorProvider
{
    public IReadOnlyList<CursorDef> Designs => CursorLibrary.All;

    public CursorImage Render(CursorSpec spec)
    {
        var def = CursorLibrary.Find(spec.Design);
        var (bmp, hx, hy) = CursorLibrary.Render(def, spec.Size, spec.Color);
        return new CursorImage(bmp, hx, hy);
    }

    public Bitmap Preview(CursorDef def, int box, int colorIndex) => CursorLibrary.Preview(def, box, colorIndex);

    public void Reload() => CursorLibrary.Refresh();
}
