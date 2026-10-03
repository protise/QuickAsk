using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace QuickAsk;

/// <summary>一条对话记录。UserDataUrl 仅在携带截图的用户消息上有值。</summary>
public sealed record ChatTurn(string Role, string Text, string? ImageDataUrl = null);

/// <summary>OpenAI 兼容的流式（SSE）对话客户端，零第三方依赖。</summary>
public sealed class ChatService : IDisposable
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async IAsyncEnumerable<string> StreamAsync(
        ProviderInfo provider, string modelId, IReadOnlyList<ChatTurn> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = provider.BaseUrl.TrimEnd().TrimEnd('/');
        if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            url += "/chat/completions";

        var messages = new List<object>(history.Count);
        foreach (var turn in history)
        {
            if (turn.ImageDataUrl != null && turn.Role == "user")
            {
                var imageUrl = provider.ImageStyle == "raw_base64"
                    ? turn.ImageDataUrl[(turn.ImageDataUrl.IndexOf(',') + 1)..]
                    : turn.ImageDataUrl;
                messages.Add(new
                {
                    role = turn.Role,
                    content = new object[]
                    {
                        new { type = "text", text = turn.Text },
                        new { type = "image_url", image_url = new { url = imageUrl } },
                    },
                });
            }
            else
            {
                messages.Add(new { role = turn.Role, content = turn.Text });
            }
        }

        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { model = modelId, messages, stream = true }),
            Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(ExtractError(resp.StatusCode, errBody));
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line.Length > 5 ? line[5..].Trim() : "";
            if (data == "[DONE]") break;

            string? delta = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("delta", out var deltaObj) &&
                    deltaObj.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    delta = content.GetString();
                }
            }
            catch
            {
                // 忽略无法解析的心跳/注释行
            }
            if (!string.IsNullOrEmpty(delta)) yield return delta;
        }
    }

    static string ExtractError(HttpStatusCode status, string body)
    {
        string msg = body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m1))
                msg = m1.GetString() ?? body;
            else if (root.TryGetProperty("message", out var m2))
                msg = m2.GetString() ?? body;
            else if (root.TryGetProperty("msg", out var m3))
                msg = m3.GetString() ?? body;
        }
        catch
        {
            // 非 JSON 错误体，原样展示（截断）
        }
        msg = msg.ReplaceLineEndings(" ").Trim();
        if (msg.Length > 160) msg = msg[..160] + "…";
        return $"HTTP {(int)status}：{msg}";
    }

    public void Dispose() => _http.Dispose();
}
