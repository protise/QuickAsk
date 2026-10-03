using System.Drawing.Imaging;

namespace QuickAsk;

/// <summary>一张待发送的截图：位图（缩略图用）+ data URL（发送用）。</summary>
public sealed record Shot(Image Image, string DataUrl);

public static class ScreenshotService
{
    /// <summary>按物理像素截取屏幕区域。rect 必须是物理坐标。</summary>
    public static Bitmap CaptureRect(Rectangle rect)
    {
        var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                g.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size);
                break;
            }
            catch when (attempt < 2)
            {
                Thread.Sleep(60); // GDI 屏幕DC偶发“句柄无效”，重试
            }
        }
        return bmp;
    }

    public static Shot ToShot(Bitmap bmp)
    {
        var (bytes, mime) = Encode(bmp);
        return new Shot(bmp, $"data:{mime};base64,{Convert.ToBase64String(bytes)}");
    }

    /// <summary>优先 PNG，超过 3.5MB 时降级 JPEG，控制请求体积。</summary>
    static (byte[] Bytes, string Mime) Encode(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        if (ms.Length <= 3_500_000) return (ms.ToArray(), "image/png");

        ms.SetLength(0);
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var quality = new EncoderParameters(1);
        quality.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
        using var tmp = new Bitmap(bmp);
        tmp.Save(ms, codec, quality);
        return (ms.ToArray(), "image/jpeg");
    }
}
