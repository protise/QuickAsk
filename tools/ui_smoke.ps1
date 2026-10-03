# UI 冒烟测试：热键呼出 -> 截图 -> 发送 -> 流式回答，每步截屏存证
# 前置：mock_server.py 已在 8787 端口；%APPDATA%\QuickAsk\config.json 已指向 mock
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public static class M {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
}
"@
function Snap([string]$name) {
    $b = New-Object System.Drawing.Bitmap(2560, 1600)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.CopyFromScreen(0, 0, 0, 0, $b.Size)
    $half = New-Object System.Drawing.Bitmap(1280, 800)
    $g2 = [System.Drawing.Graphics]::FromImage($half)
    $g2.DrawImage($b, 0, 0, 1280, 800)
    $half.Save("$env:TEMP\$name")
    $g.Dispose(); $b.Dispose(); $g2.Dispose(); $half.Dispose()
    Write-Host "saved $env:TEMP\$name"
}
function Key([string]$k) {
    [System.Windows.Forms.SendKeys]::SendWait($k)
    Start-Sleep -Milliseconds 600
}

Write-Host "1. Alt+Q 呼出小窗"
Key "%q"
Snap "qa_1_popup.png"

Write-Host "2. Alt+S 进入框选，拖拽 (700,500)->(1500,900)"
Key "%s"
Start-Sleep -Milliseconds 500
Snap "qa_2_overlay.png"
[M]::SetCursorPos(700, 500); Start-Sleep -Milliseconds 200
[M]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150
[M]::SetCursorPos(1100, 700); Start-Sleep -Milliseconds 80
[M]::SetCursorPos(1500, 900); Start-Sleep -Milliseconds 150
[M]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 700
Snap "qa_3_attached.png"

Write-Host "3. 回车发送，等流式回答"
Key "{ENTER}"
Start-Sleep -Milliseconds 3000
Snap "qa_4_answer.png"

Write-Host "4. Alt+M 切换模型（托盘气泡）+ Alt+Q 隐藏"
Key "%m"
Key "%q"
Write-Host "done"
