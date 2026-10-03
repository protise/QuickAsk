using Microsoft.Win32;

namespace QuickAsk;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains("--probe")) { SelfCheck.Probe(); return 0; }
        if (args.Contains("--selftest"))
        {
            var url = args.FirstOrDefault(a => a.StartsWith("--url="))?["--url=".Length..] ?? "http://127.0.0.1:8787/v1";
            SelfCheck.SelfTestStreamAsync(url).GetAwaiter().GetResult();
            return 0;
        }

        using var mutex = new Mutex(true, "QuickAsk_SingleInstance", out bool first);
        if (!first) return 0;

        var cfg = ConfigStore.Load();
        bool firstRun = !ConfigStore.Exists;
        if (firstRun)
        {
            using var wizard = new FirstRunForm(cfg);
            wizard.ShowDialog();
            ConfigStore.Save(cfg);
        }

        Application.Run(new AppContext(cfg));
        return 0;
    }
}

/// <summary>隐藏消息窗：只负责接收 WM_HOTKEY。</summary>
internal sealed class HotkeyWindow : Form
{
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public HotkeyManager? Manager { get; set; }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            Manager?.Dispatch(m.WParam.ToInt32());
            return;
        }
        base.WndProc(ref m);
    }
}

/// <summary>应用主体：隐藏消息窗 + 托盘 + 全局热键 + 迷你窗。</summary>
internal sealed class AppContext : ApplicationContext
{
    readonly AppConfig _cfg;
    readonly ChatService _chat = new();
    readonly Form _window;
    readonly PopupForm _popup;
    readonly HotkeyManager _hotkeys;
    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _autoStartItem;
    bool _inSettings;

    public AppContext(AppConfig cfg)
    {
        _cfg = cfg;

        _window = new HotkeyWindow
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Opacity = 0,
        };
        MainForm = _window;

        _popup = new PopupForm(_cfg, _chat, OpenSettings);
        _hotkeys = new HotkeyManager(_window);
        ((HotkeyWindow)_window).Manager = _hotkeys;

        _autoStartItem = new ToolStripMenuItem("开机自启") { CheckOnClick = true, Checked = _cfg.AutoStart };
        _autoStartItem.CheckedChanged += (s, e) => { _cfg.AutoStart = _autoStartItem.Checked; ConfigStore.Save(_cfg); AutoStartHelper.Apply(_cfg); };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("呼出小窗", null, (s, e) => TogglePopup()));
        menu.Items.Add(new ToolStripMenuItem("设置…", null, (s, e) => OpenSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (s, e) => ExitApp()));

        _tray = new NotifyIcon
        {
            Icon = AppIcons.TrayIcon(),
            Text = "快问 QuickAsk（Alt+Q）",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) TogglePopup(); };
        _tray.MouseDoubleClick += (s, e) => TogglePopup();

        RegisterHotkeys();
    }

    void RegisterHotkeys()
    {
        _hotkeys.UnregisterAll();
        bool ok1 = _hotkeys.Register(1, _cfg.Hotkeys.Toggle, TogglePopup);
        bool ok2 = _hotkeys.Register(2, _cfg.Hotkeys.Screenshot, DoScreenshot);
        bool ok3 = _hotkeys.Register(3, _cfg.Hotkeys.SwitchModel, CycleModel);
        if (!ok1 || !ok2 || !ok3)
            _tray.ShowBalloonTip(3000, "快问",
                "部分全局热键注册失败（可能被其他软件占用），请在设置中更换热键。", ToolTipIcon.Warning);
    }

    void TogglePopup()
    {
        if (_popup.Visible) _popup.Dismiss();
        else _popup.ShowPopup();
    }

    void DoScreenshot()
    {
        if (_popup.Visible) _popup.Dismiss();
        _popup.SuppressAutoHide = true;
        try
        {
            var shot = RegionOverlayForm.CaptureInteractive();
            if (shot != null) _popup.SetPendingImage(shot);
            _popup.ShowPopup();
        }
        finally
        {
            _popup.SuppressAutoHide = false;
        }
    }

    void CycleModel()
    {
        var list = _cfg.AllModels().ToList();
        if (list.Count == 0) return;
        int idx = list.FindIndex(t => t.Pi == _cfg.ActiveProvider && t.Mi == _cfg.ActiveModel);
        var next = list[(idx + 1) % list.Count];
        _cfg.ActiveProvider = next.Pi;
        _cfg.ActiveModel = next.Mi;
        ConfigStore.Save(_cfg);
        _popup.UpdateModelChip();
        if (_popup.Visible) _popup.Toast($"已切换：{next.P.Name} · {next.M.Name}");
        else _tray.ShowBalloonTip(800, "快问", $"已切换到 {next.P.Name} · {next.M.Name}", ToolTipIcon.Info);
    }

    void OpenSettings()
    {
        if (_inSettings) return;
        _inSettings = true;
        _popup.SuppressAutoHide = true;
        try
        {
            using var form = new SettingsForm(_cfg);
            form.ShowDialog(_popup);
            if (form.Saved)
            {
                ConfigStore.Save(_cfg);
                RegisterHotkeys();
                AutoStartHelper.Apply(_cfg);
                _autoStartItem.Checked = _cfg.AutoStart;
                _popup.UpdateModelChip();
            }
        }
        finally
        {
            _popup.SuppressAutoHide = false;
            _inSettings = false;
        }
    }

    void ExitApp()
    {
        _hotkeys.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _popup.Dispose();
        _chat.Dispose();
        _window.Close();
    }
}

public static class AutoStartHelper
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "QuickAsk";

    public static void Apply(AppConfig cfg)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (cfg.AutoStart && !string.IsNullOrEmpty(Environment.ProcessPath))
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // 注册表写入失败不致命
        }
    }
}
