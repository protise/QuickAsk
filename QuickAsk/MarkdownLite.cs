using System.Text;
using System.Text.RegularExpressions;

namespace QuickAsk;

/// <summary>极简 Markdown → RTF 渲染（加粗 / 斜体 / 行内代码 / 代码块 / 标题 / 列表）。</summary>
public static class MarkdownLite
{
    public static string Render(string md)
    {
        var sb = new StringBuilder(512);
        sb.Append(@"{\rtf1\ansi\ansicpg936\deff2");
        sb.Append(@"{\fonttbl{\f0\fswiss Segoe UI;}{\f1\fmodern Consolas;}{\f2\fswiss Microsoft YaHei UI;}}");
        sb.Append(@"{\colortbl;\red45\green45\blue48;\red243\green244\blue246;\red198\green62\blue48;\red150\green150\blue156;}");
        sb.Append(@"\cf1\fs21");

        bool inCode = false;
        foreach (var raw in md.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCode) { sb.Append('}'); sb.Append(@"\par"); inCode = false; }
                else { sb.Append(@"\par{\f1\fs19\cf3\highlight2 "); inCode = true; }
                continue;
            }
            if (inCode)
            {
                AppendEscaped(sb, line);
                sb.Append(@"\line");
                continue;
            }

            var t = line.Trim();
            if (t.Length == 0) { sb.Append(@"\par"); continue; }

            if (t.StartsWith("### ")) { sb.Append(@"\par"); Inline(sb, t[4..], 23, bold: true); }
            else if (t.StartsWith("## ")) { sb.Append(@"\par"); Inline(sb, t[3..], 25, bold: true); }
            else if (t.StartsWith("# ")) { sb.Append(@"\par"); Inline(sb, t[2..], 28, bold: true); }
            else if (t.StartsWith("- ") || t.StartsWith("* "))
            {
                sb.Append(@"\par\u8226? ");
                Inline(sb, t[2..], 21);
            }
            else { sb.Append(@"\par"); Inline(sb, t, 21); }
        }
        if (inCode) sb.Append('}');
        sb.Append('}');
        return sb.ToString();
    }

    static void Inline(StringBuilder sb, string text, int size, bool bold = false)
    {
        sb.Append(@"\fs").Append(size).Append(' ');
        var esc = EscapeRtf(text);
        esc = Regex.Replace(esc, "`([^`]+)`", "{\\f1\\highlight2\\cf3 $1}");
        esc = Regex.Replace(esc, @"\*\*(.+?)\*\*", "{\\b $1}");
        esc = Regex.Replace(esc, @"(?<!\*)\*([^*]+)\*(?!\*)", "{\\i $1}");
        if (bold) esc = "{\\b " + esc + "}";
        sb.Append(esc);
    }

    static void AppendEscaped(StringBuilder sb, string s)
    {
        foreach (var c in s)
        {
            if (c is '\\' or '{' or '}') { sb.Append('\\').Append(c); }
            else if (c > 127) { sb.Append("\\u").Append(unchecked((short)c)).Append('?'); }
            else sb.Append(c);
        }
    }

    static string EscapeRtf(string s)
    {
        var sb = new StringBuilder(s.Length);
        AppendEscaped(sb, s);
        return sb.ToString();
    }
}
