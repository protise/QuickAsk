$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public static class M2 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
}
"@
[System.Windows.Forms.SendKeys]::SendWait('%q')
Start-Sleep -Milliseconds 800
# 小窗宽660物理像素、水平居中、底部-20；···按钮在其右上角
$x = [int]((2560 - 660) / 2 + 594)
$y = [int](1600 - 20 - 130 + 45)
[M2]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 200
[M2]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60
[M2]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 900
$b = New-Object System.Drawing.Bitmap(2560,1600)
$g = [System.Drawing.Graphics]::FromImage($b)
$g.CopyFromScreen(0,0,0,0,$b.Size)
$half = New-Object System.Drawing.Bitmap(1280,800)
$g2 = [System.Drawing.Graphics]::FromImage($half)
$g2.DrawImage($b,0,0,1280,800)
$half.Save("$env:TEMP\qa_5_settings.png")
Write-Host 'saved'
