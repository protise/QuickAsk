using System.Runtime.InteropServices;

namespace QuickAsk;

/// <summary>
/// 全屏变暗框选层。坐标全部使用物理像素：窗口位置由 SetWindowPos 直接以物理尺寸摆放，
/// 鼠标从 WndProc 原始消息读取，从而绕开 WinForms 的 DPI 换算，保证截取内容与所见一致。
/// </summary>
public sealed class RegionOverlayForm : Form
{
    readonly Bitmap _shot;              // 覆盖整个目标显示器的原始截图（物理像素）
    readonly Rectangle _monitorPhys;    // 该显示器的物理矩形

    Point _start;                       // 客户区物理坐标
    Point _cur;
    Rectangle _sel;                     // 客户区物理坐标
    bool _dragging;
    bool _cancelled;
    bool _finished;

    public RegionOverlayForm(Bitmap shot, Rectangle monitorPhys)
    {
        _shot = shot;
        _monitorPhys = monitorPhys;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        Bounds = _monitorPhys; // 先大致就位，OnShown 再用物理值精确摆放
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW; // 不出现在 Alt-Tab
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST,
            _monitorPhys.X, _monitorPhys.Y, _monitorPhys.Width, _monitorPhys.Height,
            Native.SWP_SHOWWINDOW);
        Activate();
        _cur = new Point(_monitorPhys.Width / 2, _monitorPhys.Height / 2);
    }

    /// <summary>吞掉 DPI 变更消息，避免窗体被 WinForms 自动缩放挪走。</summary>
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case Native.WM_DPICHANGED:
                return;
            case Native.WM_LBUTTONDOWN:
                _dragging = true;
                _start = _cur = Native.PointFromLParam(m.LParam);
                _sel = Rectangle.Empty;
                Invalidate();
                return;
            case Native.WM_MOUSEMOVE:
                _cur = Native.PointFromLParam(m.LParam);
                if (_dragging)
                {
                    _sel = RectFrom(_start, _cur);
                    Invalidate();
                }
                else
                {
                    Invalidate();
                }
                return;
            case Native.WM_LBUTTONDBLCLK:
                Finish(Rectangle.Empty); // 双击 = 全屏
                return;
            case Native.WM_LBUTTONUP when _dragging:
                _dragging = false;
                var rect = RectFrom(_start, Native.PointFromLParam(m.LParam));
                if (rect.Width < 8 || rect.Height < 8) Finish(Rectangle.Empty); // 轻点 = 全屏
                else Finish(rect);
                return;
            case Native.WM_KEYDOWN when (int)m.WParam == Native.VK_ESCAPE:
                _cancelled = true;
                Close();
                return;
        }
        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { _cancelled = true; Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    static Rectangle RectFrom(Point a, Point b) =>
        Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    void Finish(Rectangle clientRect)
    {
        if (_finished) return;
        _finished = true;
        _sel = clientRect.IsEmpty ? new Rectangle(Point.Empty, _monitorPhys.Size) : clientRect;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var dim = new SolidBrush(Color.FromArgb(96, 0, 0, 0)))
            g.FillRectangle(dim, ClientRectangle);

        if (_dragging && !_sel.IsEmpty)
        {
            // 选区内绘制原始画面（“透亮”效果），因为 _shot 与窗口客户区同为物理像素，1:1 对齐
            g.DrawImage(_shot, _sel, _sel, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.FromArgb(255, 70, 70), 2f);
            g.DrawRectangle(pen, _sel);
            var label = $"{_sel.Width} × {_sel.Height}";
            using var bg = new SolidBrush(Color.FromArgb(200, 20, 20, 20));
            using var fg = new SolidBrush(Color.White);
            using var font = new Font("Microsoft YaHei UI", 9f);
            var sz = g.MeasureString(label, font);
            var lx = Math.Min(Math.Max(_sel.X, 0), _monitorPhys.Width - sz.Width - 8);
            var ly = _sel.Y - sz.Height - 6;
            if (ly < 0) ly = _sel.Bottom + 6;
            g.FillRectangle(bg, lx, ly, sz.Width + 10, sz.Height);
            g.DrawString(label, font, fg, lx + 5, ly + 1);
        }
        else
        {
            // 十字线 + 顶部提示
            using var cross = new Pen(Color.FromArgb(170, 255, 255, 255), 1f);
            g.DrawLine(cross, _cur.X, 0, _cur.X, _monitorPhys.Height);
            g.DrawLine(cross, 0, _cur.Y, _monitorPhys.Width, _cur.Y);

            const string hint = "拖拽框选 · 双击全屏 · Esc 取消";
            using var bg = new SolidBrush(Color.FromArgb(190, 20, 20, 20));
            using var fg = new SolidBrush(Color.White);
            using var font = new Font("Microsoft YaHei UI", 10f);
            var sz = g.MeasureString(hint, font);
            var x = (_monitorPhys.Width - sz.Width) / 2;
            g.FillRectangle(bg, x, 18, sz.Width + 20, sz.Height + 8);
            g.DrawString(hint, font, fg, x + 10, 22);
        }
        base.OnPaint(e);
    }

    /// <summary>阻塞显示框选层，返回用户截取的 Shot；取消则返回 null。</summary>
    public static Shot? CaptureInteractive()
    {
        Native.GetCursorPos(out var pt);
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
        var hMon = Native.MonitorFromPoint(pt, 2);
        if (hMon == IntPtr.Zero || !Native.GetMonitorInfo(hMon, ref mi)) return null;

        var monitor = Rectangle.FromLTRB(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);

        using var full = ScreenshotService.CaptureRect(monitor);
        using var overlay = new RegionOverlayForm(full, monitor);
        overlay.ShowDialog();
        return overlay.TakeResult();
    }

    /// <summary>框选结束后取结果；取消或未选择返回 null。</summary>
    public Shot? TakeResult()
    {
        if (_cancelled || _sel.Width <= 0 || _sel.Height <= 0) return null;
        using var crop = _shot.Clone(_sel, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        return ScreenshotService.ToShot(new Bitmap(crop));
    }
}
