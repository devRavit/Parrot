using System.IO;

namespace Parrot;

/// <summary>Loads/saves Config to %LOCALAPPDATA%\Parrot\config.ini.</summary>
internal sealed class IniSettingsStore : ISettingsStore
{
    public Config Load()
    {
        var c = new Config();
        try
        {
            if (!File.Exists(Config.FilePath)) return c;
            foreach (var raw in File.ReadAllLines(Config.FilePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line[..eq].Trim(), v = line[(eq + 1)..].Trim();
                switch (k)
                {
                    case "design": c.DesignName = v; break;
                    case "size": int.TryParse(v, out c.Size); break;
                    case "color": int.TryParse(v, out c.Color); break;
                    case "replaceAll": c.ReplaceAllTypes = v == "1"; break;
                    case "enabled": c.Enabled = v == "1"; break;
                    default:
                        if (k.StartsWith("app:") && (v is "inject" or "overlay" or "system" or "normal"))
                            c.AppMethod[k[4..]] = v == "normal" ? "system" : v;
                        break;
                }
            }
        }
        catch { }
        c.Size = Math.Clamp(c.Size, 0, CursorArt.SizeCount - 1);
        c.Color = Math.Clamp(c.Color, 0, CursorArt.ColorCount - 1);
        return c;
    }

    public void Save(Config c)
    {
        try
        {
            Directory.CreateDirectory(Config.Dir);
            using var w = new StreamWriter(Config.FilePath, false);
            w.WriteLine("# Parrot config");
            w.WriteLine($"design={c.DesignName}");
            w.WriteLine($"size={c.Size}");
            w.WriteLine($"color={c.Color}");
            w.WriteLine($"replaceAll={(c.ReplaceAllTypes ? 1 : 0)}");
            w.WriteLine($"enabled={(c.Enabled ? 1 : 0)}");
            foreach (var kv in c.AppMethod)
                w.WriteLine($"app:{kv.Key}={kv.Value}");
        }
        catch { }
    }
}
