namespace QuickAsk;

/// <summary>运行时生成托盘/窗体图标，免去附带资源文件。</summary>
public static class AppIcons
{
    static Icon? _cached;

    public static Icon TrayIcon()
    {
        if (_cached != null) return _cached;
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(1, 1, 14, 14, 180, 180);
            path.AddArc(17, 1, 14, 14, 270, 180);
            path.AddArc(17, 17, 14, 14, 0, 180);
            path.AddArc(1, 17, 14, 14, 90, 180);
            path.CloseFigure();
            using (var b = new SolidBrush(Color.FromArgb(31, 31, 31))) g.FillPath(b, path);
            using var f = new Font("Segoe UI", 15f, FontStyle.Bold);
            var sz = g.MeasureString("Q", f);
            using var wb = new SolidBrush(Color.White);
            g.DrawString("Q", f, wb, 16 - sz.Width / 2, 16 - sz.Height / 2);
        }
        _cached = Icon.FromHandle(bmp.GetHicon());
        return _cached;
    }
}
