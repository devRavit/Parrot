using System.IO;
using System.Reflection;
using System.Text;
using static Parrot.Native;

namespace Parrot;

/// <summary>Injects the cursor-hook DLL into target processes (CreateRemoteThread + LoadLibraryW),
/// and keeps the shared active.cur in sync for injected apps.</summary>
internal sealed class InjectionService : IInjectionService
{
    public static string InjectDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Parrot", "inject");
    private static string DllPath => Path.Combine(InjectDir, "ParrotHook64.dll");
    private static string CurPath => Path.Combine(InjectDir, "active.cur");

    private readonly HashSet<uint> _injected = new();

    public void EnsureReady()
    {
        Directory.CreateDirectory(InjectDir);
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            string? res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("ParrotHook64.dll", StringComparison.OrdinalIgnoreCase));
            if (res == null) return;
            using var s = asm.GetManifestResourceStream(res);
            if (s == null) return;
            byte[] embedded = new byte[s.Length];
            s.ReadExactly(embedded);
            bool needWrite = true;
            if (File.Exists(DllPath))
                try { needWrite = !File.ReadAllBytes(DllPath).AsSpan().SequenceEqual(embedded); } catch { needWrite = true; }
            if (needWrite)
                try { File.WriteAllBytes(DllPath, embedded); } catch { /* locked by injected process */ }
        }
        catch { }
    }

    public void WriteActiveCursor(CursorImage image)
    {
        try { Directory.CreateDirectory(InjectDir); CurWriter.Write(image.Bitmap, image.HotX, image.HotY, CurPath); }
        catch { }
    }

    public InjectionResult Inject(uint pid)
    {
        if (_injected.Contains(pid)) return InjectionResult.AlreadyInjected;
        EnsureReady();
        if (!File.Exists(DllPath)) return InjectionResult.Failed;

        uint rights = PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ | PROCESS_QUERY_INFORMATION;
        IntPtr h = OpenProcess(rights, false, pid);
        if (h == IntPtr.Zero) return InjectionResult.OpenFailed;
        try
        {
            if (IsWow64Process(h, out bool wow64) && wow64) return InjectionResult.WrongBitness;

            byte[] path = Encoding.Unicode.GetBytes(DllPath + "\0");
            IntPtr mem = VirtualAllocEx(h, IntPtr.Zero, (uint)path.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (mem == IntPtr.Zero) return InjectionResult.Failed;
            try
            {
                if (!WriteProcessMemory(h, mem, path, (uint)path.Length, out _)) return InjectionResult.Failed;
                IntPtr loadLib = GetProcAddress(GetModuleHandleW("kernel32.dll"), "LoadLibraryW");
                if (loadLib == IntPtr.Zero) return InjectionResult.Failed;
                IntPtr thread = CreateRemoteThread(h, IntPtr.Zero, 0, loadLib, mem, 0, out _);
                if (thread == IntPtr.Zero) return InjectionResult.Failed;
                WaitForSingleObject(thread, 5000);
                GetExitCodeThread(thread, out uint exitCode); // LoadLibraryW ret low32; 0 == blocked (e.g. WoW)
                CloseHandle(thread);
                if (exitCode == 0) return InjectionResult.Blocked;
                _injected.Add(pid);
                return InjectionResult.Ok;
            }
            finally { VirtualFreeEx(h, mem, 0, MEM_RELEASE); }
        }
        finally { CloseHandle(h); }
    }

    /// <summary>Inject the window's process + its child-window processes (multi-process apps like MuMu).
    /// Returns whether the main process was injected.</summary>
    public bool InjectWindowTree(IntPtr hwnd, uint mainPid)
    {
        uint ownPid = (uint)Environment.ProcessId;
        var pids = new HashSet<uint> { mainPid };
        try
        {
            EnumChildWindows(hwnd, (child, _) =>
            {
                GetWindowThreadProcessId(child, out uint cpid);
                if (cpid != 0) pids.Add(cpid);
                return true;
            }, IntPtr.Zero);
        }
        catch { }

        bool mainOk = false;
        foreach (var pid in pids)
        {
            if (pid == ownPid) continue;
            var r = Inject(pid);
            if (pid == mainPid) mainOk = r is InjectionResult.Ok or InjectionResult.AlreadyInjected;
        }
        return mainOk;
    }
}
