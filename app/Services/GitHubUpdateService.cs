using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Parrot;

/// <summary>Checks GitHub Releases for a newer version and, if found, downloads the Setup asset
/// and runs it (silent) to self-update.</summary>
internal sealed class GitHubUpdateService : IUpdateService
{
    private const string Owner = "devRavit";
    private const string Repo = "Parrot";

    private readonly HttpClient _http;
    private readonly Action _exitForUpdate;

    public GitHubUpdateService(Action exitForUpdate)
    {
        _exitForUpdate = exitForUpdate;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Parrot-Updater");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public Version Current =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    public async Task CheckAsync(bool manual)
    {
        try
        {
            string json = await _http.GetStringAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var latest = ParseVersion(root.GetProperty("tag_name").GetString());
            if (latest == null) { if (manual) Info("최신 릴리스 정보를 확인할 수 없습니다."); return; }
            if (Normalize(latest) <= Normalize(Current)) { if (manual) Info($"최신 버전입니다. (현재 v{Current.ToString(3)})"); return; }

            string? assetUrl = null, assetName = null;
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                string name = a.GetProperty("name").GetString() ?? "";
                if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                { assetUrl = a.GetProperty("browser_download_url").GetString(); assetName = name; break; }
            }
            if (assetUrl == null) { if (manual) Info($"새 버전 v{latest.ToString(3)} 이 있으나 설치 파일을 찾지 못했습니다."); return; }

            // Automatic: download and install silently (no prompt), then exit so the installer swaps.
            string tmp = Path.Combine(Path.GetTempPath(), assetName!);
            using (var resp = await _http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead))
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                await resp.Content.CopyToAsync(fs);

            Process.Start(new ProcessStartInfo(tmp, "--silent") { UseShellExecute = true });
            OnUi(_exitForUpdate);
        }
        catch (Exception ex) { if (manual) Info("업데이트 확인 실패: " + ex.Message); }
    }

    private static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var m = Regex.Match(tag, @"\d+(\.\d+){1,3}");
        return m.Success && Version.TryParse(m.Value, out var v) ? v : null;
    }

    /// <summary>Compare only Major.Minor.Build (avoid Version's -1 revision quirks).</summary>
    private static Version Normalize(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));

    private static void OnUi(Action a) => System.Windows.Application.Current?.Dispatcher.Invoke(a);
    private static void Info(string s) => OnUi(() => MessageBox.Show(s, "Parrot 업데이트", MessageBoxButton.OK, MessageBoxImage.Information));
}
