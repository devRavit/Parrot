using Microsoft.Win32;

namespace Parrot;

/// <summary>Windows startup registration via the HKCU Run key.</summary>
internal sealed class AutoStartService : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "Parrot";

    public bool IsEnabled
    {
        get
        {
            try { using var k = Registry.CurrentUser.OpenSubKey(RunKey, false); return k?.GetValue(RunName) is string; }
            catch { return false; }
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (k == null) return;
            if (enabled) k.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue(RunName, false);
        }
        catch { }
    }
}
