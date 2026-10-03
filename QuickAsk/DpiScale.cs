namespace QuickAsk;

public static class DpiScale
{
    /// <summary>把对话框按系统缩放整体放大几何尺寸（字体为磅值会自动缩放，无需处理）。</summary>
    public static void Apply(Form f)
    {
        float k = f.DeviceDpi / 96f;
        if (k <= 1.01f) return;
        f.ClientSize = new Size((int)(f.ClientSize.Width * k), (int)(f.ClientSize.Height * k));
        Walk(f.Controls, k);
    }

    static void Walk(Control.ControlCollection controls, float k)
    {
        foreach (Control c in controls)
        {
            c.Location = new Point((int)(c.Location.X * k), (int)(c.Location.Y * k));
            if (c is not Label) c.Size = new Size((int)(c.Size.Width * k), (int)(c.Size.Height * k));
            Walk(c.Controls, k);
        }
    }
}
