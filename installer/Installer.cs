using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ParrotSetup;

internal static class Program
{
    public const string AppName = "Parrot";
    public const string ExeName = "Parrot.exe";
    public const string Version = "2.0.1";
    public const string RunName = "Parrot";
    public const string ArpKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Parrot";

    public static string DefaultInstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Parrot");

    [STAThread]
    private static void Main(string[] args)
    {
        bool silent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
        bool uninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));

        // Silent modes run without any UI (useful for deployment / automated tests).
        if (silent && uninstall) { Engine.Uninstall(_ => { }); return; }
        if (silent)
        {
            var o = new InstallOptions
            {
                Dir = DefaultInstallDir,
                Desktop = true, StartMenu = true, AutoStart = true, Launch = true
            };
            Engine.Install(o, _ => { });
            return;
        }

        ApplicationConfiguration.Initialize();
        if (uninstall) Application.Run(new UninstallForm());
        else Application.Run(new WizardForm());
    }
}

internal sealed class InstallOptions
{
    public string Dir = "";
    public bool Desktop, StartMenu, AutoStart, Launch;
}

// ------------------------------------------------------------------ Install/Uninstall engine
internal static class Engine
{
    /// <summary>Perform the installation. Returns the installed exe path.</summary>
    public static string Install(InstallOptions o, Action<string> log)
    {
        string dir = string.IsNullOrWhiteSpace(o.Dir) ? Program.DefaultInstallDir : o.Dir.Trim();
        string exePath = Path.Combine(dir, Program.ExeName);

        foreach (var p in Process.GetProcessesByName("Parrot"))
        { try { p.Kill(true); p.WaitForExit(3000); } catch { } }

        log("폴더 생성: " + dir);
        Directory.CreateDirectory(dir);

        log("프로그램 파일 복사...");
        ExtractPayload(exePath);
        log("  " + exePath);

        string uninstaller = Path.Combine(dir, "uninstall.exe");
        try { File.Copy(Environment.ProcessPath!, uninstaller, true); } catch { }

        if (o.StartMenu)
        {
            string sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Program.AppName);
            Directory.CreateDirectory(sm);
            Shortcuts.Create(Path.Combine(sm, Program.AppName + ".lnk"), exePath, "", dir, exePath);
            Shortcuts.Create(Path.Combine(sm, "Uninstall " + Program.AppName + ".lnk"), uninstaller, "--uninstall", dir, uninstaller);
            log("시작 메뉴 등록됨");
        }
        if (o.Desktop)
        {
            string dt = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Shortcuts.Create(Path.Combine(dt, Program.AppName + ".lnk"), exePath, "", dir, exePath);
            log("바탕화면 바로가기 생성됨");
        }
        if (o.AutoStart)
        {
            using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            k?.SetValue(Program.RunName, $"\"{exePath}\"");
            log("자동 시작 등록됨 (HKCU\\...\\Run)");
        }

        using (var k = Registry.CurrentUser.CreateSubKey(Program.ArpKey))
        {
            k?.SetValue("DisplayName", Program.AppName);
            k?.SetValue("DisplayVersion", Program.Version);
            k?.SetValue("Publisher", "Parrot");
            k?.SetValue("InstallLocation", dir);
            k?.SetValue("DisplayIcon", exePath);
            k?.SetValue("UninstallString", $"\"{uninstaller}\" --uninstall");
            k?.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        log("프로그램 추가/제거 등록됨");

        if (o.Launch)
        {
            try { Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = dir, UseShellExecute = true }); log("프로그램을 실행했습니다."); }
            catch (Exception ex) { log("실행 실패: " + ex.Message); }
        }
        return exePath;
    }

    public static void Uninstall(Action<string> log)
    {
        foreach (var p in Process.GetProcessesByName("Parrot"))
        { try { p.Kill(true); p.WaitForExit(3000); } catch { } }

        try { using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true); run?.DeleteValue(Program.RunName, false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(Program.ArpKey, false); } catch { }
        log("레지스트리 정리됨");

        string sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Program.AppName);
        try { if (Directory.Exists(sm)) Directory.Delete(sm, true); } catch { }
        string dt = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        try { File.Delete(Path.Combine(dt, Program.AppName + ".lnk")); } catch { }
        log("바로가기 제거됨");

        try
        {
            string cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Parrot");
            if (Directory.Exists(cfg)) Directory.Delete(cfg, true);
        }
        catch { }

        // install dir (may contain the running uninstaller) -> delayed removal
        string? dir = Path.GetDirectoryName(Environment.ProcessPath!);
        if (dir != null && Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir))
                if (!string.Equals(f, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                    try { File.Delete(f); } catch { }
            var psi = new ProcessStartInfo("cmd.exe",
                $"/c ping 127.0.0.1 -n 2 >nul & rmdir /s /q \"{dir}\"")
            { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
            try { Process.Start(psi); } catch { }
        }
        log("프로그램 파일 제거됨");
    }

    private static void ExtractPayload(string destExe)
    {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? s = asm.GetManifestResourceStream("Parrot.exe")
            ?? throw new InvalidOperationException("내장된 프로그램 파일을 찾을 수 없습니다.");
        using var fs = new FileStream(destExe, FileMode.Create, FileAccess.Write);
        s.CopyTo(fs);
    }
}

// ------------------------------------------------------------------ Wizard UI
internal sealed class WizardForm : Form
{
    private readonly Panel _body = new() { Dock = DockStyle.Fill, Padding = new Padding(28, 18, 28, 18) };
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 68 };
    private readonly Panel _footer = new() { Dock = DockStyle.Bottom, Height = 56 };
    private readonly Button _btnNext = new() { Text = "설치", Width = 110, Height = 32 };
    private readonly Button _btnCancel = new() { Text = "취소", Width = 90, Height = 32 };

    private TextBox _txtPath = null!;
    private CheckBox _cbDesktop = null!, _cbStartMenu = null!, _cbAuto = null!, _cbLaunch = null!;
    private int _page;

    public WizardForm()
    {
        Text = $"{Program.AppName} 설치";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 420);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.White;
        TopMost = true;

        _header.Paint += PaintHeader;
        _footer.BackColor = Color.FromArgb(245, 246, 248);

        _btnNext.Click += (_, _) => OnNext();
        _btnCancel.Click += (_, _) => Close();
        _btnNext.Location = new Point(ClientSize.Width - 28 - _btnNext.Width, 12);
        _btnCancel.Location = new Point(_btnNext.Left - 10 - _btnCancel.Width, 12);
        _btnNext.Anchor = _btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        _footer.Controls.Add(_btnNext);
        _footer.Controls.Add(_btnCancel);
        AcceptButton = _btnNext;

        Controls.Add(_body);
        Controls.Add(_footer);
        Controls.Add(_header);

        Shown += (_, _) => { TopMost = false; Activate(); };
        ShowOptionsPage();
    }

    private void PaintHeader(object? s, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new LinearGradientBrush(_header.ClientRectangle,
            Color.FromArgb(28, 32, 40), Color.FromArgb(46, 54, 68), LinearGradientMode.Horizontal);
        g.FillRectangle(bg, _header.ClientRectangle);
        var pts = new PointF[] { new(24, 16), new(24, 50), new(34, 42), new(41, 56), new(47, 53), new(40, 40), new(53, 40) };
        using var br = new SolidBrush(Color.FromArgb(80, 255, 60));
        using var pen = new Pen(Color.FromArgb(230, 15, 15, 15), 2f) { LineJoin = LineJoin.Round };
        g.FillPolygon(br, pts); g.DrawPolygon(pen, pts);
        using var tb = new SolidBrush(Color.White);
        using var f1 = new Font("Segoe UI Semibold", 13f);
        using var f2 = new Font("Segoe UI", 8.5f);
        g.DrawString($"{Program.AppName} 설치 마법사", f1, tb, 68, 14);
        using var tb2 = new SolidBrush(Color.FromArgb(190, 200, 215));
        g.DrawString($"버전 {Program.Version} · 런타임 내장(추가 설치 불필요)", f2, tb2, 70, 40);
    }

    private void ClearBody() => _body.Controls.Clear();

    private void ShowOptionsPage()
    {
        _page = 0;
        ClearBody();
        _btnNext.Text = "설치";

        _body.Controls.Add(new Label
        {
            Text = "설치 옵션을 선택하고 [설치]를 누르세요.",
            AutoSize = true, Location = new Point(0, 4),
            Font = new Font("Segoe UI Semibold", 10.5f)
        });

        _body.Controls.Add(new Label { Text = "설치 위치", AutoSize = true, Location = new Point(0, 44) });
        _txtPath = new TextBox { Text = Program.DefaultInstallDir, Location = new Point(0, 66), Width = 400 };
        var btnBrowse = new Button { Text = "찾아보기...", Location = new Point(408, 64), Width = 96, Height = 26 };
        btnBrowse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = _txtPath.Text };
            if (d.ShowDialog(this) == DialogResult.OK)
                _txtPath.Text = Path.Combine(d.SelectedPath, "Parrot");
        };
        _body.Controls.Add(_txtPath);
        _body.Controls.Add(btnBrowse);

        _cbDesktop = new CheckBox { Text = "바탕화면 바로가기 만들기", Checked = true, AutoSize = true, Location = new Point(0, 108) };
        _cbStartMenu = new CheckBox { Text = "시작 메뉴에 등록", Checked = true, AutoSize = true, Location = new Point(0, 136) };
        _cbAuto = new CheckBox { Text = "Windows 시작 시 자동 실행", Checked = true, AutoSize = true, Location = new Point(0, 164) };
        _cbLaunch = new CheckBox { Text = "설치 완료 후 바로 실행", Checked = true, AutoSize = true, Location = new Point(0, 192) };
        _body.Controls.AddRange(new Control[] { _cbDesktop, _cbStartMenu, _cbAuto, _cbLaunch });

        _body.Controls.Add(new Label
        {
            Text = "• 관리자 권한이 필요 없는 사용자 설치입니다.\n" +
                   "• 커서 오버레이는 대부분의 툴과 창모드/테두리없는 게임에서 동작합니다.",
            AutoSize = false, Location = new Point(0, 230), Size = new Size(504, 60),
            ForeColor = Color.FromArgb(90, 96, 105)
        });
    }

    private void OnNext()
    {
        if (_page == 0) DoInstall();
        else Close();
    }

    private void DoInstall()
    {
        _btnNext.Enabled = _btnCancel.Enabled = false;
        ClearBody();
        var status = new Label { Text = "설치 중...", AutoSize = true, Location = new Point(0, 8), Font = new Font("Segoe UI Semibold", 10.5f) };
        var log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(0, 40), Size = new Size(504, 250), BackColor = Color.White };
        _body.Controls.Add(status);
        _body.Controls.Add(log);
        void Log(string s) { log.AppendText(s + "\r\n"); Application.DoEvents(); }

        try
        {
            var o = new InstallOptions
            {
                Dir = _txtPath.Text,
                Desktop = _cbDesktop.Checked,
                StartMenu = _cbStartMenu.Checked,
                AutoStart = _cbAuto.Checked,
                Launch = _cbLaunch.Checked
            };
            Engine.Install(o, Log);
            status.Text = "설치 완료!";
            Log("");
            Log("설치가 완료되었습니다.");
        }
        catch (Exception ex)
        {
            status.Text = "설치 실패";
            Log("오류: " + ex.Message);
        }

        _page = 1;
        _btnNext.Text = "완료";
        _btnNext.Enabled = true;
        _btnCancel.Visible = false;
    }
}

// ------------------------------------------------------------------ Uninstall UI
internal sealed class UninstallForm : Form
{
    public UninstallForm()
    {
        var res = MessageBox.Show($"{Program.AppName}을(를) 제거하시겠습니까?",
            Program.AppName + " 제거", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        Load += (_, _) =>
        {
            if (res == DialogResult.Yes)
            {
                try { Engine.Uninstall(_ => { }); MessageBox.Show($"{Program.AppName}이(가) 제거되었습니다.", Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (Exception ex) { MessageBox.Show("제거 중 오류: " + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            Close();
        };
        Opacity = 0; ShowInTaskbar = false;
    }
}

// ------------------------------------------------------------------ Shortcuts (WScript.Shell)
internal static class Shortcuts
{
    public static void Create(string lnkPath, string target, string args, string workDir, string iconPath)
    {
        try
        {
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic sc = shell.CreateShortcut(lnkPath);
            sc.TargetPath = target;
            sc.Arguments = args;
            sc.WorkingDirectory = workDir;
            sc.IconLocation = iconPath + ",0";
            sc.WindowStyle = 1;
            sc.Description = Program.AppName;
            sc.Save();
        }
        catch { }
    }
}

