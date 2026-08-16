using static Parrot.Native;

namespace Parrot;

/// <summary>Applies a full custom cursor scheme (one image per mouse-event state) via
/// SetSystemCursor. Installs each cursor once — never on mouse movement — to avoid resize-border
/// flicker (repeated SetSystemCursor strobes the global cursor).</summary>
internal sealed class SystemCursorService : ISystemCursorService
{
    private static readonly uint[] AllIds =
    {
        OCR_NORMAL, OCR_HAND, OCR_APPSTARTING, OCR_HELP, OCR_UP,
        OCR_IBEAM, OCR_CROSS, OCR_WAIT, OCR_SIZEALL,
        OCR_SIZENWSE, OCR_SIZENESW, OCR_SIZEWE, OCR_SIZENS, OCR_NO
    };

    private readonly Dictionary<uint, IntPtr> _templates = new();
    private IntPtr _blank = IntPtr.Zero;

    public bool Active { get; private set; }

    public void Apply(IReadOnlyDictionary<uint, CursorImage> scheme)
    {
        DestroyTemplates();
        foreach (var kv in scheme)
        {
            IntPtr t = CursorHandles.Build(kv.Value.Bitmap, kv.Value.HotX, kv.Value.HotY);
            kv.Value.Dispose();               // service takes ownership of the images
            if (t == IntPtr.Zero) continue;
            _templates[kv.Key] = t;
            IntPtr copy = CopyIcon(t);        // SetSystemCursor destroys the handle it receives
            if (copy != IntPtr.Zero) SetSystemCursor(copy, kv.Key);
        }
        Active = true;
    }

    public void Reassert()
    {
        foreach (var kv in _templates)
        {
            IntPtr copy = CopyIcon(kv.Value);
            if (copy != IntPtr.Zero) SetSystemCursor(copy, kv.Key);
        }
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
        foreach (var id in AllIds)
        {
            IntPtr c = CopyIcon(_blank);
            if (c != IntPtr.Zero) SetSystemCursor(c, id);
        }
        Active = true;
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
        DestroyTemplates();
        Active = false;
    }

    private void DestroyTemplates()
    {
        foreach (var t in _templates.Values) DestroyCursor(t);
        _templates.Clear();
    }
}
