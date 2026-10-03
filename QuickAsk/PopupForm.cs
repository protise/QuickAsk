using System.Runtime.InteropServices;
using System.Text;

namespace QuickAsk;

/// <summary>迷你问答窗：无边框置顶小条，Enter 发送，流式回答向下展开。</summary>
public sealed class PopupForm : Form
{
    readonly float _s;
    readonly int _w, _headerH, _inputH, _statusH, _iconBtn;

    int R(float v) => (int)Math.Round(v * _s);

    readonly AppConfig _cfg;
    readonly ChatService _chat;
    readonly Action _openSettings;
    readonly List<ChatTurn> _history = new();
    readonly StringBuilder _answer = new();

    readonly Label _modelChip;
    readonly Label _hint;
    readonly Label _status;
    readonly RichTextBox _answerBox;
    readonly TextBox _input;
    readonly PictureBox _thumb;
    readonly Button _clearBtn, _settingsBtn, _sendBtn;
    readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 4000 };

    Image? _pendingImage;
    string? _pendingDataUrl;
    CancellationTokenSource? _cts;
    bool _streaming;

    int _lastRenderTick;
    int _anchorBottom;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool SuppressAutoHide { get; set; }

    public PopupForm(AppConfig cfg, ChatService chat, Action openSettings)
    {
        _cfg = cfg;
        _chat = chat;
        _openSettings = openSettings;
        _s = DeviceDpi / 96f;
        _w = R(440);
        _headerH = R(40);
        _inputH = R(46);
        _statusH = R(24);
        _iconBtn = R(30);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 10f);
        Size = new Size(_w, _headerH + _inputH);

        _modelChip = new Label
        {
            AutoSize = true,
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(96, 96, 96),
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
            Location = new Point(R(14), R(11)),
        };
        _modelChip.Click += (s, e) => ShowModelMenu();
        Controls.Add(_modelChip);

        _clearBtn = MakeIconBtn("×", "清空对话");
        _clearBtn.Click += (s, e) => ClearConversation();
        _settingsBtn = MakeIconBtn("···", "设置");
        _settingsBtn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _settingsBtn.Click += (s, e) => _openSettings();
        Controls.Add(_clearBtn);
        Controls.Add(_settingsBtn);

        _answerBox = new RichTextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ReadOnly = true,
            DetectUrls = false,
            Font = new Font("Microsoft YaHei UI", 10f),
            Visible = false,
            TabStop = false,
            ScrollBars = RichTextBoxScrollBars.None,
        };
        Controls.Add(_answerBox);
        _answerBox.BringToFront();

        _status = new Label
        {
            AutoSize = false,
            Height = R(24),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 8, 0),
            ForeColor = Color.FromArgb(190, 62, 48),
            Font = new Font("Microsoft YaHei UI", 8.5f),
            Visible = false,
        };
        Controls.Add(_status);

        _thumb = new PictureBox
        {
            Size = new Size(R(34), R(34)),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false,
            Cursor = Cursors.Hand,
        };
        _thumb.Click += (s, e) => ClearPendingImage();
        Controls.Add(_thumb);

        _hint = new Label
        {
            Text = "输入问题，Enter 发送 · Alt+S 截图",
            ForeColor = Color.FromArgb(176, 176, 176),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9.5f),
            Cursor = Cursors.IBeam,
        };
        Controls.Add(_hint);
        _hint.BringToFront();

        _input = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 10.5f),
        };
        _input.KeyDown += InputKeyDown;
        _input.TextChanged += (s, e) => _hint.Visible = _input.Text.Length == 0;
        Controls.Add(_input);
        _hint.MouseDown += (s, e) => _input.Focus();

        _sendBtn = new Button
        {
            Text = "↑",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(R(32), R(32)),
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(31, 31, 31),
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        _sendBtn.FlatAppearance.BorderSize = 0;
        _sendBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
        _sendBtn.Click += (s, e) => { if (_streaming) _cts?.Cancel(); else Send(); };
        Controls.Add(_sendBtn);

        _toastTimer.Tick += (s, e) => { _toastTimer.Stop(); _status.Visible = false; PerformLayoutNow(); };

        Deactivate += (s, e) => { if (!SuppressAutoHide) BeginInvoke(Dismiss); };

        PerformLayoutNow();
    }

    Button MakeIconBtn(string text, string tip)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(_iconBtn, _iconBtn),
            Font = new Font("Segoe UI", 12f),
            ForeColor = Color.FromArgb(120, 120, 120),
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(242, 242, 242);
        return b;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int pref = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Color.FromArgb(226, 226, 226));
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var line = new Pen(Color.FromArgb(240, 240, 240));
        if (_answerBox.Visible) e.Graphics.DrawLine(line, 0, _headerH, Width, _headerH);
        e.Graphics.DrawLine(line, 0, Height - _inputH, Width, Height - _inputH);
    }

    // ---------- 布局 ----------

    void PerformLayoutNow()
    {
        var wa = SafeWorkArea();
        int statusH = _status.Visible ? _statusH : 0;
        int answerH = MeasureAnswer(wa);
        _answerBox.Visible = answerH > 0;
        int h = _headerH + answerH + statusH + _inputH;

        _clearBtn.Location = new Point(Width - R(14) - _iconBtn * 2 - R(6), (_headerH - _iconBtn) / 2);
        _settingsBtn.Location = new Point(Width - R(14) - _iconBtn, (_headerH - _iconBtn) / 2);
        _answerBox.SetBounds(1, _headerH, Width - 2, answerH);
        _status.SetBounds(1, _headerH + answerH, Width - 2, statusH);

        int inputTop = _headerH + answerH + statusH;
        _thumb.Location = new Point(R(12), inputTop + (_inputH - _thumb.Height) / 2);
        _sendBtn.Location = new Point(Width - R(12) - R(32), inputTop + (_inputH - R(32)) / 2);
        int inputLeft = _thumb.Visible ? R(56) : R(16);
        _input.SetBounds(inputLeft, inputTop + (_inputH - _input.Height) / 2,
            Width - inputLeft - R(48) - R(14), _input.Height);
        _hint.Location = new Point(inputLeft, inputTop + (_inputH - _hint.Height) / 2);

        if (_anchorBottom == 0) _anchorBottom = wa.Bottom - R(20);
        SetBounds(Left, _anchorBottom - h, Width, h);
        Invalidate();
    }

    Rectangle SafeWorkArea()
    {
        try { return Screen.FromPoint(Cursor.Position).WorkingArea; }
        catch { return Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1040); }
    }

    int MeasureAnswer(Rectangle wa)
    {
        if (_answer.Length == 0) return 0;
        int y = _answerBox.GetPositionFromCharIndex(Math.Max(0, _answerBox.TextLength - 1)).Y;
        int h = y + _answerBox.Font.Height + 18;
        int cap = (int)(wa.Height * 0.6) - _headerH - _inputH - _statusH;
        if (h > cap)
        {
            h = cap;
            if (_answerBox.ScrollBars != RichTextBoxScrollBars.Vertical)
                _answerBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        }
        else if (_answerBox.ScrollBars != RichTextBoxScrollBars.None)
        {
            _answerBox.ScrollBars = RichTextBoxScrollBars.None;
        }
        return Math.Max(h, 40);
    }

    // ---------- 显示 / 隐藏 ----------

    public void ShowPopup()
    {
        var wa = SafeWorkArea();
        _anchorBottom = wa.Bottom - R(20);
        Left = wa.Left + (wa.Width - Width) / 2;
        UpdateModelChip();
        PerformLayoutNow();
        if (!Visible) Show();
        Activate();
        _input.Focus();
    }

    public void Dismiss()
    {
        if (_streaming) _cts?.Cancel();
        Hide();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Dismiss(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- 模型 ----------

    public void UpdateModelChip()
    {
        var (_, m) = _cfg.Current();
        _modelChip.Text = m?.Name ?? "未配置模型";
    }

    void ShowModelMenu()
    {
        var menu = new ContextMenuStrip();
        foreach (var (p, m, pi, mi) in _cfg.AllModels())
        {
            var item = new ToolStripMenuItem($"{p.Name} · {m.Name}{(m.Vision ? "（识图）" : "")}")
            {
                Checked = pi == _cfg.ActiveProvider && mi == _cfg.ActiveModel,
            };
            int a = pi, b = mi;
            item.Click += (s, e) =>
            {
                _cfg.ActiveProvider = a;
                _cfg.ActiveModel = b;
                UpdateModelChip();
                ConfigStore.Save(_cfg);
            };
            menu.Items.Add(item);
        }
        menu.Show(_modelChip, new Point(0, _modelChip.Height + 2));
    }

    // ---------- 截图 ----------

    public void SetPendingImage(Shot shot)
    {
        _pendingImage?.Dispose();
        _pendingImage = shot.Image;
        _pendingDataUrl = shot.DataUrl;
        _thumb.Image = shot.Image;
        _thumb.Visible = true;
        _hint.Visible = false;
        PerformLayoutNow();
        _input.Focus();
    }

    void ClearPendingImage()
    {
        _pendingImage?.Dispose();
        _pendingImage = null;
        _pendingDataUrl = null;
        _thumb.Image = null;
        _thumb.Visible = false;
        _hint.Visible = true;
        PerformLayoutNow();
    }

    void AttachClipboardImage()
    {
        if (!Clipboard.ContainsImage()) return;
        using var src = Clipboard.GetImage();
        if (src == null) return;
        using var bmp = new Bitmap(src);
        SetPendingImage(ScreenshotService.ToShot(new Bitmap(bmp)));
    }

    // ---------- 发送 ----------

    void InputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            Send();
        }
        else if (e.KeyCode == Keys.Back && _input.Text.Length == 0 && _pendingImage != null)
        {
            e.SuppressKeyPress = true;
            ClearPendingImage();
        }
        else if (e.Control && e.KeyCode == Keys.V && Clipboard.ContainsImage())
        {
            e.SuppressKeyPress = true;
            AttachClipboardImage();
        }
    }

    async void Send()
    {
        if (_streaming) return;
        var (p, m) = _cfg.Current();
        if (p == null || m == null) { Toast("尚未配置模型，请先打开设置"); _openSettings(); return; }
        if (string.IsNullOrWhiteSpace(p.ApiKey)) { Toast("请先在设置中填写 API Key"); _openSettings(); return; }

        var text = _input.Text.Trim();
        if (text.Length == 0 && _pendingDataUrl == null) return;
        if (_pendingDataUrl != null && !m.Vision)
        {
            Toast($"「{m.Name}」不支持识图，按 Alt+M 切换模型，或点缩略图移除");
            return;
        }

        _history.Add(new ChatTurn("user", text.Length > 0 ? text : "请解释这张截图", _pendingDataUrl));
        _input.Clear();
        ClearPendingImage();

        _answer.Clear();
        RenderAnswer(true);
        SetStreaming(true);
        _cts = new CancellationTokenSource();
        try
        {
            await foreach (var delta in _chat.StreamAsync(p, m.Id, _history, _cts.Token))
            {
                _answer.Append(delta);
                RenderAnswer();
            }
            _history.Add(new ChatTurn("assistant", _answer.ToString()));
            RenderAnswer(true);
        }
        catch (OperationCanceledException)
        {
            Toast("已停止");
        }
        catch (Exception ex)
        {
            Toast(ex.Message.Length > 90 ? ex.Message[..90] + "…" : ex.Message);
        }
        finally
        {
            SetStreaming(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    void SetStreaming(bool on)
    {
        _streaming = on;
        _sendBtn.Text = on ? "■" : "↑";
    }

    void RenderAnswer(bool force = false)
    {
        int tick = Environment.TickCount;
        if (!force && tick - _lastRenderTick < 80) return;
        _lastRenderTick = tick;
        if (_answer.Length == 0) _answerBox.Clear();
        else _answerBox.Rtf = MarkdownLite.Render(_answer.ToString());
        PerformLayoutNow();
    }

    void ClearConversation()
    {
        if (_streaming) _cts?.Cancel();
        _history.Clear();
        _answer.Clear();
        RenderAnswer(true);
    }

    public void Toast(string msg)
    {
        _status.Text = msg;
        _status.Visible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
        PerformLayoutNow();
    }
}
