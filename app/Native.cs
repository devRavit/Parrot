using System.Runtime.InteropServices;
using System.Text;

namespace Parrot;

internal static class Native
{
    // System cursor ids (OCR_*)
    public const uint OCR_NORMAL = 32512;
    public const uint OCR_IBEAM = 32513;
    public const uint OCR_WAIT = 32514;
    public const uint OCR_CROSS = 32515;
    public const uint OCR_UP = 32516;
    public const uint OCR_SIZENWSE = 32642;
    public const uint OCR_SIZENESW = 32643;
    public const uint OCR_SIZEWE = 32644;
    public const uint OCR_SIZENS = 32645;
    public const uint OCR_SIZEALL = 32646;
    public const uint OCR_NO = 32648;
    public const uint OCR_HAND = 32649;
    public const uint OCR_APPSTARTING = 32650;
    public const uint OCR_HELP = 32651;

    public const uint SPI_SETCURSORS = 0x0057;
    public const uint SPIF_SENDCHANGE = 0x02;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // WinEvent
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    // ---- overlay window (layered, click-through, topmost) ----
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOOWNERZORDER = 0x0200;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;

    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx; public int cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll", SetLastError = true)] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hDC);

    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // user32
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetSystemCursor(IntPtr hcur, uint id);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadCursorFromFile(string lpFileName);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CopyIcon(IntPtr hIcon);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyCursor(IntPtr hCursor);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
    [DllImport("user32.dll")]
    public static extern IntPtr SetCursor(IntPtr hCursor);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // gdi32
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    // ---- injection ----
    public const uint PROCESS_CREATE_THREAD = 0x0002;
    public const uint PROCESS_VM_OPERATION = 0x0008;
    public const uint PROCESS_VM_WRITE = 0x0020;
    public const uint PROCESS_VM_READ = 0x0010;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint MEM_COMMIT = 0x1000;
    public const uint MEM_RESERVE = 0x2000;
    public const uint MEM_RELEASE = 0x8000;
    public const uint PAGE_READWRITE = 0x04;
    public const uint INFINITE = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string lpModuleName);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
    [DllImport("kernel32.dll")]
    public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64);

    // kernel32
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    public static string GetProcessExe(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return "";
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                var sb = new StringBuilder(1024);
                uint size = 1024;
                if (QueryFullProcessImageName(h, 0, sb, ref size))
                    return System.IO.Path.GetFileName(sb.ToString()).ToLowerInvariant();
            }
            finally { CloseHandle(h); }
        }
        catch { }
        return "";
    }
}
