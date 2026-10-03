namespace QuickAsk;

/// <summary>首次运行向导：引导用户粘贴智谱免费 API Key。</summary>
public sealed class FirstRunForm : Form
{
    public FirstRunForm(AppConfig cfg)
    {
        Text = "欢迎使用快问 QuickAsk";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true; // 即便正在全屏看视频也能看到
        Font = new Font("Microsoft YaHei UI", 10f);
        ClientSize = new Size(470, 250);

        var title = new Label
        {
            Text = "快问 · 全局热键 AI 问答",
            Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold),
            Location = new Point(24, 18),
            AutoSize = true,
        };
        var body = new Label
        {
            Text = "1. 打开 open.bigmodel.cn（智谱开放平台），手机号注册\n2. 在「API Keys」页面创建一个 Key（免费，无需绑卡）\n3. 粘贴到下面，之后按 Alt+Q 呼出小窗即可提问",
            Location = new Point(24, 60),
            AutoSize = true,
            ForeColor = Color.FromArgb(90, 90, 90),
        };
        var key = new TextBox
        {
            Location = new Point(24, 142),
            Width = 422,
            PlaceholderText = "粘贴 API Key，例如 0a1b2c….AbCdEf",
        };
        var ok = new Button { Text = "开始使用", Location = new Point(24, 186), Size = new Size(130, 36) };
        var skip = new LinkLabel { Text = "稍后在设置中配置", Location = new Point(172, 194), AutoSize = true };

        ok.Click += (s, e) =>
        {
            if (cfg.Providers.Count > 0) cfg.Providers[0].ApiKey = key.Text.Trim();
            Close();
        };
        skip.Click += (s, e) => Close();

        Controls.AddRange(new Control[] { title, body, key, ok, skip });
        AcceptButton = ok;
        DpiScale.Apply(this);
    }
}
