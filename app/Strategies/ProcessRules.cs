namespace Parrot;

/// <summary>Our own process — leave native (System baseline).</summary>
internal sealed class SelfRule : IProcessRule
{
    private readonly string _self = System.IO.Path.GetFileName(Environment.ProcessPath ?? "").ToLowerInvariant();
    public bool TryClassify(string exe, out StrategyKind kind)
    {
        kind = StrategyKind.System;
        return exe.Equals(_self, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Shell / system UI — don't inject; SetSystemCursor already covers them.</summary>
internal sealed class SystemShellRule : IProcessRule
{
    private static readonly HashSet<string> Shell = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe","dwm.exe","applicationframehost.exe","searchhost.exe","searchapp.exe",
        "startmenuexperiencehost.exe","shellexperiencehost.exe","textinputhost.exe","sihost.exe",
        "taskmgr.exe","lockapp.exe","systemsettings.exe","rundll32.exe","ctfmon.exe"
    };
    public bool TryClassify(string exe, out StrategyKind kind) { kind = StrategyKind.System; return Shell.Contains(exe); }
}

/// <summary>Strong anti-cheat / cursor-locking games — never inject (ban safety), overlay would
/// freeze, so leave native.</summary>
internal sealed class AntiCheatRule : IProcessRule
{
    private static readonly HashSet<string> Games = new(StringComparer.OrdinalIgnoreCase)
    {
        "wow.exe","wowclassic.exe","wowt.exe",
        "valorant.exe","valorant-win64-shipping.exe","fortniteclient-win64-shipping.exe",
        "r5apex.exe","cs2.exe","csgo.exe","tslgame.exe","destiny2.exe","eldenring.exe","gta5.exe",
        "leagueclient.exe","league of legends.exe"
    };
    public bool TryClassify(string exe, out StrategyKind kind) { kind = StrategyKind.System; return Games.Contains(exe); }
}
