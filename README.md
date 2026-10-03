# 快问 QuickAsk

全局热键迷你 AI 问答窗：正在看视频、看网页时遇到不懂的画面，按热键呼出小窗 → 热键截图 → 回车，AI 直接解释。极度简洁、常驻托盘、单文件 238KB。

![架构](https://img.shields.io/badge/.NET-9_WinForms-blue) ![大小](https://img.shields.io/badge/exe-238KB-success)

## 功能与默认热键（均可在设置中修改）

| 热键 | 功能 |
|---|---|
| `Alt+Q` | 呼出 / 隐藏迷你窗（屏幕底部居中，自动聚焦输入框） |
| `Alt+S` | 截图：全屏变暗，拖拽框选；双击或轻点 = 全屏；`Esc` 取消 |
| `Alt+M` | 在已配置的模型间循环切换（弹气泡提示） |
| `Enter` | 发送（`Shift+Enter` 换行）；`Esc` 隐藏小窗 |

- 截图会以缩略图挂在输入框左侧，点缩略图或空输入时按退格可移除；也支持 `Ctrl+V` 直接粘贴剪贴板图片。
- 回答流式输出，支持多轮追问；点 `×` 清空上下文；点 `···` 打开设置；点击小窗以外的任意位置自动隐藏。
- 托盘图标：左键呼出小窗；右键菜单 = 呼出 / 设置 / 开机自启 / 退出。

## 首次使用（免费）

1. 打开 [open.bigmodel.cn](https://open.bigmodel.cn)（智谱开放平台），手机号注册。
2. 在「API Keys」页面创建一个 Key —— **完全免费，无需绑卡**。
3. 首次运行 QuickAsk 会弹出引导，粘贴 Key 即可。默认配置：
   - `glm-4-flash`：纯文字问答
   - `glm-4v-flash`：**支持识图**（默认选中，配合截图使用）

> 任何 OpenAI 兼容的服务（硅基流动、Gemini 免费层、自建网关等）都能接入：设置 → 供应商 → 填接口地址（以 `/v1` 结尾，如 `https://open.bigmodel.cn/api/paas/v4`）和 Key → 添加模型。

## 典型场景

看电视时看不懂某个画面：`Alt+Q` 呼出 → `Alt+S` 框选画面 → 回车 → AI 解释，全程手不离键盘。

## 构建 / 发布

```bash
# 开发运行
dotnet run -c Release

# 发布单文件 exe（约 238KB，需系统已装 .NET 9 桌面运行时）
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
# 产物在 bin/Release/net9.0-windows/win-x64/publish/QuickAsk.exe
```

配置文件位置：`%APPDATA%\QuickAsk\config.json`（设置界面里改即可，一般不用手编）。

## 开发自测工具

```bash
python tools/mock_server.py            # 本地 mock SSE 服务（127.0.0.1:8787）
QuickAsk.exe --probe                   # 打印 DPI/显示器坐标诊断 + 截屏验证
QuickAsk.exe --selftest --url=http://127.0.0.1:8787/v1   # 流式管线自测
tools/ui_smoke.ps1                     # 模拟键鼠的全流程 UI 冒烟测试
```

## 技术要点

- C# WinForms / .NET 9，零第三方 NuGet 依赖；全局热键 = `RegisterHotKey`；截图 = GDI `CopyFromScreen`。
- 全程物理像素坐标（PerMonitorV2 + `AutoScaleMode.None` + 手动 DPI 缩放），多屏/高 DPI 下截取内容与所见一致。
- 流式对话：`HttpClient` 直读 SSE，逐字渲染进 RichTextBox（自写 Markdown→RTF：加粗/斜体/行内代码/代码块/标题/列表）。

## 路线图

- [ ] 网页版账号逆向适配器（配置 Cookie/Token 直连网页版 AI，架构已预留：`ChatService` 按供应商抽象）
- [ ] 对话历史保存
- [ ] 更多供应商预设（硅基流动 / Gemini / Ollama 本地模型）
