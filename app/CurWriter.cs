using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Parrot;

/// <summary>Writes a 32bpp .cur (cursor) file from a straight-alpha bitmap and hotspot, so the
/// injected hook DLL can LoadCursorFromFile it inside the target process.</summary>
internal static class CurWriter
{
    public static void Write(Bitmap bmp, int hotX, int hotY, string path)
    {
        int w = bmp.Width, h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] pixels = new byte[data.Stride * h];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        int stride = data.Stride;
        bmp.UnlockBits(data);

        int andRow = ((w + 31) / 32) * 4;      // 1bpp mask row, dword-aligned
        int xorSize = w * h * 4;
        int andSize = andRow * h;
        int dib = 40 + xorSize + andSize;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICONDIR
        bw.Write((ushort)0);        // reserved
        bw.Write((ushort)2);        // type = cursor
        bw.Write((ushort)1);        // count
        // ICONDIRENTRY
        bw.Write((byte)(w >= 256 ? 0 : w));
        bw.Write((byte)(h >= 256 ? 0 : h));
        bw.Write((byte)0);          // color count
        bw.Write((byte)0);          // reserved
        bw.Write((ushort)hotX);     // hotspot X (planes field)
        bw.Write((ushort)hotY);     // hotspot Y (bitcount field)
        bw.Write((uint)dib);        // bytes in resource
        bw.Write((uint)22);         // offset (6 + 16)

        // BITMAPINFOHEADER
        bw.Write((uint)40);
        bw.Write(w);
        bw.Write(h * 2);            // height doubled (XOR + AND)
        bw.Write((ushort)1);       // planes
        bw.Write((ushort)32);      // bpp
        bw.Write((uint)0);         // BI_RGB
        bw.Write((uint)xorSize);
        bw.Write(0); bw.Write(0);
        bw.Write((uint)0); bw.Write((uint)0);

        // XOR pixels, bottom-up. Memory is BGRA which is exactly the .cur order.
        for (int y = h - 1; y >= 0; y--)
        {
            int off = y * stride;
            bw.Write(pixels, off, w * 4);
        }
        // AND mask, all zero (alpha channel governs transparency)
        var zero = new byte[andSize];
        bw.Write(zero);
    }
}
