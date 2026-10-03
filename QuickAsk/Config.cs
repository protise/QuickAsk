using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickAsk;

public class ModelInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Vision { get; set; }
}

public class ProviderInfo
{
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<ModelInfo> Models { get; set; } = new();

    /// <summary>图片编码方式：openai = data URL（默认）；raw_base64 = 纯 base64（部分国产平台旧格式）。</summary>
    public string ImageStyle { get; set; } = "openai";
}

public class HotkeyConfig
{
    public string Toggle { get; set; } = "Alt+Q";
    public string Screenshot { get; set; } = "Alt+S";
    public string SwitchModel { get; set; } = "Alt+M";
}

public class AppConfig
{
    public List<ProviderInfo> Providers { get; set; } = new();
    public int ActiveProvider { get; set; }
    public int ActiveModel { get; set; }
    public HotkeyConfig Hotkeys { get; set; } = new();
    public bool AutoStart { get; set; }

    [JsonIgnore]
    public bool HasKey => Providers.Any(p => p.Enabled && !string.IsNullOrWhiteSpace(p.ApiKey));

    public (ProviderInfo? P, ModelInfo? M) Current()
    {
        if (ActiveProvider < 0 || ActiveProvider >= Providers.Count) return (null, null);
        var p = Providers[ActiveProvider];
        if (ActiveModel < 0 || ActiveModel >= p.Models.Count) return (p, null);
        return (p, p.Models[ActiveModel]);
    }

    public IEnumerable<(ProviderInfo P, ModelInfo M, int Pi, int Mi)> AllModels()
    {
        for (int i = 0; i < Providers.Count; i++)
        {
            var p = Providers[i];
            if (!p.Enabled) continue;
            for (int j = 0; j < p.Models.Count; j++)
                yield return (p, p.Models[j], i, j);
        }
    }
}

public static class ConfigStore
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickAsk");
    public static readonly string FilePath = Path.Combine(Dir, "config.json");

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static bool Exists => File.Exists(FilePath);

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath));
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // 配置损坏时回退默认，避免应用无法启动
        }
        return Default();
    }

    public static void Save(AppConfig cfg)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(cfg, Options));
    }

    public static AppConfig Default() => new()
    {
        ActiveModel = 1, // 默认选 GLM-4V-Flash：支持截图识图，开箱即用
        Providers =
        {
            new ProviderInfo
            {
                Name = "智谱 GLM",
                BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
                Models =
                {
                    new ModelInfo { Id = "glm-4-flash", Name = "GLM-4-Flash" },
                    new ModelInfo { Id = "glm-4v-flash", Name = "GLM-4V-Flash", Vision = true },
                },
            },
        },
    };
}
