using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Parrot.Native;

namespace Parrot;

/// <summary>Topmost, click-through, layered window that draws the cursor and follows the mouse.
/// Used for apps that block DLL injection. 32bpp DIB section for correct per-pixel alpha.</summary>
internal sealed class OverlayService : IOverlayService
{
    private OverlayForm? _form;
    private IntPtr _hook;
    private HookProc? _proc;
    private int _w, _h, _hotX, _hotY;
    private bool _painted;

    public bool Visible => _form is { Visible: true };

    public void Show(CursorImage image)
    {
        Update(image);
        EnsureForm();
        if (!_form!.Visible) _form.Show();
        if (_hook == IntPtr.Zero)
        {
            _proc = MouseProc;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        }
        MoveToCursor();
    }

    public void Update(CursorImage image)
    {
        EnsureForm();
        _hotX = image.HotX; _hotY = image.HotY; _w = image.Bitmap.Width; _h = image.Bitmap.Height;

        byte[] pm = PremultipliedBytes(image.Bitmap);
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        var bi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = _w, biHeight = -_h, biPlanes = 1, biBitCount = 32, biCompression = 0
        };
        IntPtr hBmp = CreateDIBSection(memDc, ref bi, 0, out IntPtr bits, IntPtr.Zero, 0);
        IntPtr old = IntPtr.Zero;
        try
        {
            if (hBmp != IntPtr.Zero && bits != IntPtr.Zero)
            {
                Marshal.Copy(pm, 0, bits, pm.Length);
                old = SelectObject(memDc, hBmp);
                var size = new SIZE { cx = _w, cy = _h };
                var src = new POINT { x = 0, y = 0 };
                var dst = new POINT { x = _form!.Left, y = _form.Top };
                var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
                UpdateLayeredWindow(_form.Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
                _painted = true;
            }
        }
        finally
        {
            if (old != IntPtr.Zero) SelectObject(memDc, old);
            if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
        MoveToCursor();
    }

    public void Hide()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        _form?.Hide();
    }

    public void Dispose() { Hide(); _form?.Dispose(); _form = null; }

    private void EnsureForm()
    {
        if (_form != null) return;
        _form = new OverlayForm();
        _form.CreateControl();
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_MOUSEMOVE && _painted) MoveToCursor();
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void MoveToCursor()
    {
        if (_form == null || !_painted) return;
        if (GetCursorPos(out POINT p))
            SetWindowPos(_form.Handle, HWND_TOPMOST, p.x - _hotX, p.y - _hotY, _w, _h,
                SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_SHOWWINDOW);
    }

    private static byte[] PremultipliedBytes(Bitmap src)
    {
        int w = src.Width, h = src.Height;
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); g.DrawImageUnscaled(src, 0, 0); }
        var rect = new Rectangle(0, 0, w, h);
        var d = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] outBytes = new byte[w * h * 4];
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(d.Scan0 + y * d.Stride, outBytes, y * w * 4, w * 4);
        }
        finally { bmp.UnlockBits(d); }
        for (int i = 0; i < outBytes.Length; i += 4)
        {
            byte a = outBytes[i + 3];
            if (a == 255) continue;
            outBytes[i] = (byte)(outBytes[i] * a / 255);
            outBytes[i + 1] = (byte)(outBytes[i + 1] * a / 255);
            outBytes[i + 2] = (byte)(outBytes[i + 2] * a / 255);
        }
        return outBytes;
    }

    private sealed class OverlayForm : Form
    {
        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-4000, -4000, 1, 1);
            Text = "ParrotOverlay";
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }
    }
}
