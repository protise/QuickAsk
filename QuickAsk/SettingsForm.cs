using System.ComponentModel;

namespace QuickAsk;

/// <summary>设置窗：供应商/模型/热键/开机自启。极度朴素的表单，只求好用。</summary>
public sealed class SettingsForm : Form
{
    readonly AppConfig _cfg;
    ProviderInfo? _provider;
    ModelInfo? _model;
    bool _loading;

    public bool Saved { get; private set; }

    ComboBox _provCombo = null!;
    TextBox _baseUrl = null!, _apiKey = null!;
    ListBox _modelList = null!;
    TextBox _modelId = null!, _modelName = null!;
    CheckBox _vision = null!, _autoStart = null!;
    TextBox _hkToggle = null!, _hkScreenshot = null!, _hkSwitch = null!;

    public SettingsForm(AppConfig cfg)
    {
        _cfg = cfg;

        Text = "快问 · 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true; // 保证在全屏视频之上可见
        Font = new Font("Microsoft YaHei UI", 9.5f);
        ClientSize = new Size(520, 640);

        int labelX = 24, fieldW = 470;

        var lblProv = new Label { Text = "供应商", Location = new Point(labelX, 26), AutoSize = true };
        _provCombo = new ComboBox { Location = new Point(100, 22), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        var btnAddProv = new Button { Text = "新增", Location = new Point(410, 20), Size = new Size(48, 26) };
        var btnDelProv = new Button { Text = "删除", Location = new Point(462, 20), Size = new Size(48, 26) };
        _provCombo.SelectedIndexChanged += (s, e) => LoadProvider();
        btnAddProv.Click += (s, e) =>
        {
            var p = new ProviderInfo { Name = $"供应商 {_cfg.Providers.Count + 1}", BaseUrl = "https://" };
            _cfg.Providers.Add(p);
            RebuildProviders();
            _provCombo.SelectedIndex = _cfg.Providers.Count - 1;
        };
        btnDelProv.Click += (s, e) =>
        {
            if (_provider == null || _cfg.Providers.Count <= 1) return;
            if (MessageBox.Show(this, $"确定删除供应商「{_provider.Name}」及其所有模型？", "确认",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int idx = _cfg.Providers.IndexOf(_provider);
            _cfg.Providers.RemoveAt(idx);
            _cfg.ActiveProvider = Math.Clamp(_cfg.ActiveProvider, 0, _cfg.Providers.Count - 1);
            RebuildProviders();
        };
        Controls.AddRange(new Control[] { lblProv, _provCombo, btnAddProv, btnDelProv });

        var lblUrl = new Label { Text = "接口地址", Location = new Point(labelX, 64), AutoSize = true };
        _baseUrl = new TextBox { Location = new Point(100, 60), Width = fieldW - 76 };
        _baseUrl.TextChanged += (s, e) => { if (!_loading && _provider != null) _provider.BaseUrl = _baseUrl.Text; };
        var lblKey = new Label { Text = "API Key", Location = new Point(labelX, 100), AutoSize = true };
        _apiKey = new TextBox { Location = new Point(100, 96), Width = fieldW - 76, UseSystemPasswordChar = true };
        _apiKey.TextChanged += (s, e) => { if (!_loading && _provider != null) _provider.ApiKey = _apiKey.Text; };
        Controls.AddRange(new Control[] { lblUrl, _baseUrl, lblKey, _apiKey });

        var grpModel = new Label
        {
            Text = "模型（Alt+M 在此列表中循环切换）",
            Location = new Point(labelX, 136),
            AutoSize = true,
            ForeColor = Color.FromArgb(110, 110, 110),
        };
        Controls.Add(grpModel);

        _modelList = new ListBox { Location = new Point(100, 158), Width = 300, Height = 110 };
        var btnAddModel = new Button { Text = "新增", Location = new Point(410, 158), Size = new Size(48, 26) };
        var btnDelModel = new Button { Text = "删除", Location = new Point(410, 190), Size = new Size(48, 26) };
        _modelList.SelectedIndexChanged += (s, e) => LoadModel();
        btnAddModel.Click += (s, e) =>
        {
            if (_provider == null) return;
            var m = new ModelInfo { Id = "model-id", Name = "新模型" };
            _provider.Models.Add(m);
            RebuildModels();
            _modelList.SelectedIndex = _provider.Models.Count - 1;
        };
        btnDelModel.Click += (s, e) =>
        {
            if (_provider == null || _model == null) return;
            int idx = _provider.Models.IndexOf(_model);
            _provider.Models.Remove(_model);
            if (_cfg.ActiveProvider == _cfg.Providers.IndexOf(_provider))
                _cfg.ActiveModel = Math.Clamp(_cfg.ActiveModel, 0, Math.Max(0, _provider.Models.Count - 1));
            RebuildModels();
        };
        Controls.AddRange(new Control[] { _modelList, btnAddModel, btnDelModel });

        var lblModelId = new Label { Text = "模型 ID", Location = new Point(labelX, 286), AutoSize = true };
        _modelId = new TextBox { Location = new Point(100, 282), Width = 220 };
        _modelId.TextChanged += (s, e) => { if (!_loading && _model != null) _model.Id = _modelId.Text; };
        var lblModelName = new Label { Text = "显示名", Location = new Point(336, 286), AutoSize = true };
        _modelName = new TextBox { Location = new Point(396, 282), Width = 114 };
        _modelName.TextChanged += (s, e) => { if (!_loading && _model != null) _modelName_TextChanged(_model); };
        _vision = new CheckBox { Text = "支持识图（可接收截图）", Location = new Point(100, 316), AutoSize = true };
        _vision.CheckedChanged += (s, e) => { if (!_loading && _model != null) _model.Vision = _vision.Checked; };
        Controls.AddRange(new Control[] { lblModelId, _modelId, lblModelName, _modelName, _vision });

        var lblHotkey = new Label
        {
            Text = "全局热键（点击输入框后直接按键）",
            Location = new Point(labelX, 354),
            AutoSize = true,
            ForeColor = Color.FromArgb(110, 110, 110),
        };
        Controls.Add(lblHotkey);

        _hkToggle = HotkeyBox("唤起/隐藏", 382);
        _hkScreenshot = HotkeyBox("截 图", 418);
        _hkSwitch = HotkeyBox("切换模型", 454);

        _autoStart = new CheckBox { Text = "开机自动启动", Location = new Point(100, 492), AutoSize = true };
        Controls.Add(_autoStart);

        var btnSave = new Button { Text = "保存", Location = new Point(100, 560), Size = new Size(120, 36) };
        btnSave.Click += (s, e) => Save();
        var btnCancel = new Button { Text = "取消", Location = new Point(236, 560), Size = new Size(120, 36) };
        btnCancel.Click += (s, e) => Close();
        Controls.AddRange(new Control[] { btnSave, btnCancel });

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        RebuildProviders();
        DpiScale.Apply(this);
    }

    TextBox HotkeyBox(string label, int y)
    {
        var lbl = new Label { Text = label, Location = new Point(24, y + 4), AutoSize = true };
        var box = new TextBox { Location = new Point(100, y), Width = 160, ReadOnly = true, BackColor = Color.White };
        box.KeyDown += (s, e) =>
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
            box.Text = HotkeyCombo.FromKeyEventArgs(e);
        };
        Controls.AddRange(new Control[] { lbl, box });
        return box;
    }

    void RebuildProviders()
    {
        _loading = true;
        _provCombo.Items.Clear();
        foreach (var p in _cfg.Providers) _provCombo.Items.Add(p.Name);
        _loading = false;
        int idx = Math.Clamp(_cfg.ActiveProvider, 0, _cfg.Providers.Count - 1);
        _provCombo.SelectedIndex = idx;
    }

    void LoadProvider()
    {
        _loading = true;
        _provider = _cfg.Providers[_provCombo.SelectedIndex];
        _baseUrl.Text = _provider.BaseUrl;
        _apiKey.Text = _provider.ApiKey;
        _loading = false;
        RebuildModels();
    }

    void RebuildModels()
    {
        _loading = true;
        _modelList.Items.Clear();
        if (_provider != null)
            foreach (var m in _provider.Models)
                _modelList.Items.Add($"{m.Name}  ({m.Id}){(m.Vision ? "  [识图]" : "")}");
        _loading = false;
        if (_provider != null && _provider.Models.Count > 0)
            _modelList.SelectedIndex = Math.Clamp(_cfg.ActiveModel, 0, _provider.Models.Count - 1);
        else
            LoadModel();
    }

    void LoadModel()
    {
        _loading = true;
        _model = _provider != null && _modelList.SelectedIndex >= 0 ? _provider.Models[_modelList.SelectedIndex] : null;
        _modelId.Text = _model?.Id ?? "";
        _modelName.Text = _model?.Name ?? "";
        _vision.Checked = _model?.Vision ?? false;
        bool has = _model != null;
        _modelId.Enabled = _modelName.Enabled = _vision.Enabled = has;
        _loading = false;
    }

    void _modelName_TextChanged(ModelInfo m)
    {
        m.Name = _modelName.Text;
        int idx = _modelList.SelectedIndex;
        if (idx >= 0)
            _modelList.Items[idx] = $"{m.Name}  ({m.Id}){(m.Vision ? "  [识图]" : "")}";
    }

    void Save()
    {
        // 校验热键
        foreach (var (label, text) in new[]
        {
            ("唤起/隐藏", _hkToggle.Text), ("截图", _hkScreenshot.Text), ("切换模型", _hkSwitch.Text),
        })
        {
            if (HotkeyCombo.Parse(text) == null)
            {
                MessageBox.Show(this, $"热键「{label}」无效：{text}", "快问", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        _cfg.Hotkeys.Toggle = _hkToggle.Text;
        _cfg.Hotkeys.Screenshot = _hkScreenshot.Text;
        _cfg.Hotkeys.SwitchModel = _hkSwitch.Text;
        _cfg.AutoStart = _autoStart.Checked;
        _cfg.ActiveProvider = _provCombo.SelectedIndex;
        if (_modelList.SelectedIndex >= 0) _cfg.ActiveModel = _modelList.SelectedIndex;
        Saved = true;
        Close();
    }
}
