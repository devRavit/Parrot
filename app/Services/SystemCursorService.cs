using static Parrot.Native;

namespace Parrot;

/// <summary>Replaces / blanks / restores the Windows system cursors (SetSystemCursor).</summary>
internal sealed class SystemCursorService : ISystemCursorService
{
    private static readonly uint[] PointerIds = { OCR_NORMAL, OCR_HAND, OCR_APPSTARTING, OCR_HELP };
    private static readonly uint[] AllIds =
    {
        OCR_NORMAL, OCR_HAND, OCR_APPSTARTING, OCR_HELP, OCR_UP,
        OCR_IBEAM, OCR_CROSS, OCR_WAIT, OCR_SIZEALL,
        OCR_SIZENWSE, OCR_SIZENESW, OCR_SIZEWE, OCR_SIZENS, OCR_NO
    };

    private IntPtr _template = IntPtr.Zero;
    private IntPtr _blank = IntPtr.Zero;
    private bool _replaceAll;

    public bool Active { get; private set; }

    public void Apply(CursorImage image, bool replaceAllTypes)
    {
        IntPtr fresh = CursorHandles.Build(image.Bitmap, image.HotX, image.HotY);
        if (fresh == IntPtr.Zero) return;
        if (_template != IntPtr.Zero) DestroyCursor(_template);
        _template = fresh;
        _replaceAll = replaceAllTypes;
        SetIds(_template, replaceAllTypes ? AllIds : PointerIds);
        Active = true;
    }

    public void Blank()
    {
        if (_blank == IntPtr.Zero)
        {
            var and = new byte[32 * 32 / 8];
            var xor = new byte[32 * 32 / 8];
            for (int i = 0; i < and.Length; i++) { and[i] = 0xFF; xor[i] = 0x00; }
            _blank = CreateCursor(GetModuleHandle(null), 0, 0, 32, 32, and, xor);
        }
        if (_blank == IntPtr.Zero) return;
        SetIds(_blank, AllIds);   // blank everything so nothing (incl. I-beam) shows under an overlay
        Active = true;
    }

    public void Reassert()
    {
        if (_template == IntPtr.Zero) return;
        SetIds(_template, _replaceAll ? AllIds : PointerIds);
    }

    public void Restore()
    {
        try
        {
            var map = new (uint id, string name)[]
            {
                (OCR_NORMAL, "Arrow"), (OCR_HAND, "Hand"), (OCR_APPSTARTING, "AppStarting"),
                (OCR_HELP, "Help"), (OCR_CROSS, "Crosshair"), (OCR_SIZEALL, "SizeAll"), (OCR_UP, "UpArrow"),
                (OCR_IBEAM, "IBeam"), (OCR_WAIT, "Wait"),
                (OCR_SIZENWSE, "SizeNWSE"), (OCR_SIZENESW, "SizeNESW"), (OCR_SIZEWE, "SizeWE"),
                (OCR_SIZENS, "SizeNS"), (OCR_NO, "No")
            };
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
            foreach (var m in map)
            {
                string? path = key?.GetValue(m.name) as string;
                if (string.IsNullOrEmpty(path)) continue;
                path = Environment.ExpandEnvironmentVariables(path);
                IntPtr h = LoadCursorFromFile(path);
                if (h != IntPtr.Zero) SetSystemCursor(h, m.id);
            }
        }
        catch { }
        try { SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE); } catch { }
        Active = false;
    }

    private static void SetIds(IntPtr template, uint[] ids)
    {
        foreach (var id in ids)
        {
            IntPtr copy = CopyIcon(template); // SetSystemCursor destroys the handle it receives
            if (copy != IntPtr.Zero) SetSystemCursor(copy, id);
        }
    }
}
