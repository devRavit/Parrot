using System.IO;

namespace Parrot;

/// <summary>Persisted settings (data model only). IO is handled by ISettingsStore.</summary>
public sealed class Config
{
    public string DesignName = "Arrow";
    public int Size = 3;                 // 0..4 (Large)
    public int Color = 5;               // palette index (White)
    public bool ReplaceAllTypes = false;
    public bool Enabled = true;

    // learned per-app strategy: exe(lowercase) -> "system" | "inject" | "overlay"
    public readonly Dictionary<string, string> AppMethod = new(StringComparer.OrdinalIgnoreCase);

    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Parrot");
    public static string FilePath => Path.Combine(Dir, "config.ini");
}
