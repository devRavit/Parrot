using System.Drawing;
using static Parrot.Native;

namespace Parrot;

/// <summary>Builds native HCURSOR handles from bitmaps. Shared by the system-cursor service and
/// the live in-app cursor preview.</summary>
internal static class CursorHandles
{
    public static IntPtr Build(Bitmap bmp, int hotX, int hotY)
    {
        IntPtr hColor = bmp.GetHbitmap(Color.FromArgb(0)); // 32bpp straight alpha
        IntPtr hMask = CreateBitmap(bmp.Width, bmp.Height, 1, 1, IntPtr.Zero);
        var ii = new ICONINFO { fIcon = false, xHotspot = hotX, yHotspot = hotY, hbmMask = hMask, hbmColor = hColor };
        IntPtr h = CreateIconIndirect(ref ii);
        DeleteObject(hColor);
        DeleteObject(hMask);
        return h;
    }
}
