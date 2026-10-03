using System.ComponentModel;

namespace QuickAsk;

/// <summary>"Alt+Q" / "Ctrl+Shift+Space" 风格的热键描述与 Win32 注册参数互转。</summary>
public readonly record struct HotkeyCombo(uint Mods, uint Vk)
{
    public static HotkeyCombo? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        uint mods = 0;
        uint vk = 0;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "alt": mods |= Native.MOD_ALT; continue;
                case "ctrl" or "control": mods |= Native.MOD_CONTROL; continue;
                case "shift": mods |= Native.MOD_SHIFT; continue;
                case "win" or "windows": mods |= Native.MOD_WIN; continue;
            }
            try
            {
                var key = (Keys)new KeysConverter().ConvertFromString(part)!;
                vk = (uint)key & 0xFFFF;
            }
            catch
            {
                return null;
            }
        }
        if (vk == 0) return null;
        return new HotkeyCombo(mods, vk);
    }

    /// <summary>由按键事件生成描述字符串，用于设置界面录键。</summary>
    public static string FromKeyEventArgs(KeyEventArgs e)
    {
        var parts = new List<string>();
        if (e.Control) parts.Add("Ctrl");
        if (e.Alt) parts.Add("Alt");
        if (e.Shift) parts.Add("Shift");
        if ((e.Modifiers & (Keys.LWin | Keys.RWin)) != 0) parts.Add("Win");
        parts.Add(new KeysConverter().ConvertToString(e.KeyCode) ?? e.KeyCode.ToString());
        return string.Join("+", parts);
    }
}

/// <summary>RegisterHotKey 封装：挂在一个隐藏窗口上接收 WM_HOTKEY。</summary>
public sealed class HotkeyManager : IDisposable
{
    readonly Control _target;
    readonly Dictionary<int, Action> _actions = new();

    public HotkeyManager(Control target) => _target = target;

    public bool Register(int id, string combo, Action action)
    {
        var hk = HotkeyCombo.Parse(combo);
        if (hk == null) return false;
        if (!Native.RegisterHotKey(_target.Handle, id, hk.Value.Mods | Native.MOD_NOREPEAT, hk.Value.Vk))
            return false;
        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        if (!_target.IsHandleCreated) return;
        foreach (var id in _actions.Keys)
            Native.UnregisterHotKey(_target.Handle, id);
        _actions.Clear();
    }

    public void Dispatch(int id)
    {
        if (_actions.TryGetValue(id, out var action)) action();
    }

    public void Dispose() => UnregisterAll();
}
