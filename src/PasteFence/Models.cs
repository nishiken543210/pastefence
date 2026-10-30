using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasteFence;

public sealed class PasteFenceConfig
{
    [JsonPropertyName("originalAccessSeconds")]
    public int OriginalAccessSeconds { get; set; } = 10;

    [JsonPropertyName("logRetentionDays")]
    public int LogRetentionDays { get; set; } = 30;

    [JsonPropertyName("rules")]
    public List<ProtectionRule> Rules { get; set; } = [];

    public static PasteFenceConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("設定ファイルが見つかりません。", path);

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<PasteFenceConfig>(json, JsonOptions())
            ?? throw new InvalidDataException("設定ファイルを読み取れませんでした。");

        if (config.Rules.Count == 0)
            throw new InvalidDataException("設定ファイルに有効なルールがありません。");

        config.OriginalAccessSeconds = Math.Clamp(config.OriginalAccessSeconds, 1, 300);
        config.LogRetentionDays = Math.Clamp(config.LogRetentionDays, 1, 365);
        return config;
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, JsonOptions());
        File.WriteAllText(path, json);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}

public sealed class ProtectionRule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "新しいルール";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("matchMode")]
    public string MatchMode { get; set; } = "all";

    [JsonIgnore]
    public string MatchModeDisplay
    {
        get => MatchMode.Equals("any", StringComparison.OrdinalIgnoreCase) ? "OR（どれか一致）" : "AND（すべて一致）";
        set => MatchMode = value.StartsWith("OR", StringComparison.OrdinalIgnoreCase) ? "any" : "all";
    }

    [JsonPropertyName("conditions")]
    public List<ProtectionCondition> Conditions { get; set; } = [];
}

public sealed class ProtectionCondition
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "条件";

    [JsonPropertyName("matchType")]
    public string MatchType { get; set; } = "contains";

    [JsonIgnore]
    public string MatchTypeDisplay
    {
        get => MatchType.Equals("regex", StringComparison.OrdinalIgnoreCase) ? "正規表現" : "文字を含む";
        set => MatchType = value == "正規表現" ? "regex" : "contains";
    }

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("replaceWith")]
    public string ReplaceWith { get; set; } = "[保護]";
}

public sealed record MatchDetail(string RuleName, string Label, bool Matched, string Reason);

public sealed record ProtectionResult(
    bool Matched,
    string SafeText,
    IReadOnlyList<string> MatchedRules,
    IReadOnlyList<string> MatchedLabels,
    IReadOnlyList<MatchDetail> Details);
