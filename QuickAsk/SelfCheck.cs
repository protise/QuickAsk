using System.Diagnostics;

namespace QuickAsk;

/// <summary>自检工具：--probe 校验 DPI/坐标体系；--selftest 验证流式管线。</summary>
public static class SelfCheck
{
    public static void Probe()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Screens: {Screen.AllScreens.Length}");
        foreach (var s in Screen.AllScreens)
            sb.AppendLine($"  {s.DeviceName} Bounds={s.Bounds} Work={s.WorkingArea} Primary={s.Primary}");

        Native.GetCursorPos(out var pt);
        sb.AppendLine($"Cursor(physical): {pt.X},{pt.Y}");

        var mi = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MONITORINFO)) };
        var hMon = Native.MonitorFromPoint(pt, 2);
        if (hMon != IntPtr.Zero && Native.GetMonitorInfo(hMon, ref mi))
        {
            var w = mi.rcMonitor.Right - mi.rcMonitor.Left;
            var h = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
            sb.AppendLine($"Physical monitor: ({mi.rcMonitor.Left},{mi.rcMonitor.Top}) {w}x{h}");
            try
            {
                using var bmp = ScreenshotService.CaptureRect(
                    Rectangle.FromLTRB(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom));
                var file = Path.Combine(Path.GetTempPath(), "quickask_probe.png");
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                sb.AppendLine($"Capture: {file} ({bmp.Width}x{bmp.Height})");
            }
            catch (Exception ex) { sb.AppendLine($"Capture FAILED: {ex.Message}"); }
        }
        // 窗体坐标体系测试：WinForms 设置的 Location/Size 到底是 DIP 还是物理像素
        try
        {
            using var f = new Form { StartPosition = FormStartPosition.Manual, AutoScaleMode = AutoScaleMode.None };
            var handle = f.Handle;
            f.Location = new Point(100, 100);
            f.Size = new Size(440, 200);
            Native.GetWindowRect(f.Handle, out var r);
            sb.AppendLine($"Form coord test: set(100,100 440x200) physical=({r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top}) DeviceDpi={f.DeviceDpi}");
        }
        catch (Exception ex) { sb.AppendLine($"Form coord test FAILED: {ex.Message}"); }

        var txt = Path.Combine(Path.GetTempPath(), "quickask_probe.txt");
        File.WriteAllText(txt, sb.ToString());
        Console.Write(sb.ToString());
    }

    public static async Task SelfTestStreamAsync(string baseUrl)
    {
        var report = Path.Combine(Path.GetTempPath(), "quickask_selftest.txt");
        try
        {
            var provider = new ProviderInfo { Name = "mock", BaseUrl = baseUrl, ApiKey = "test" };
            using var chat = new ChatService();
            var history = new List<ChatTurn>
            {
                new("user", "你好"),
                new("user", "看看这张图", "data:image/png;base64,AAAA"),
            };
            var sb = new System.Text.StringBuilder();
            await foreach (var delta in chat.StreamAsync(provider, "mock-model", history, CancellationToken.None))
                sb.Append(delta);
            File.WriteAllText(report, $"OK\nURL: {baseUrl}\nAnswer: {sb}");
        }
        catch (Exception ex)
        {
            File.WriteAllText(report, $"FAILED\n{ex}");
        }
        Console.Write(File.ReadAllText(report));
    }

    public static void OpenInExplorer(string path) => Process.Start("explorer.exe", $"/select,\"{path}\"");
}
