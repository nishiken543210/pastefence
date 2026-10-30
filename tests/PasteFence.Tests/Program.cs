using PasteFence;

var failures = new List<string>();

Run("既定サンプルをAND判定して安全版へ置換", () =>
{
    var config = LoadConfig();
    var original = "顧客名A\r\n電話番号：080-1234-5678\r\n明日の打ち合わせは10時です。";
    var result = RuleEngine.Evaluate(original, config);
    Assert(result.Matched, "一致しませんでした。");
    Assert(result.MatchedRules.Contains("顧客名と電話番号"), "ルール名が結果にありません。");
    Assert(!result.SafeText.Contains("顧客名A"), "顧客名が安全版に残っています。");
    Assert(!result.SafeText.Contains("080-1234-5678"), "電話番号が安全版に残っています。");
});

Run("AND条件が片方だけなら保護しない", () =>
{
    var result = RuleEngine.Evaluate("顧客名Aだけを記載", LoadConfig());
    Assert(!result.Matched, "AND条件なのに一致しました。");
});

Run("OR条件はいずれか一つで保護", () =>
{
    var result = RuleEngine.Evaluate("この資料は社外秘です", LoadConfig());
    Assert(result.Matched, "OR条件が一致しませんでした。");
    Assert(result.SafeText.Contains("[機密表示を保護]"), "置換結果が不正です。");
});

Run("不正な正規表現を設定時に拒否", () =>
{
    var config = LoadConfig();
    config.Rules[0].Conditions[1].Pattern = "[";
    try
    {
        RuleEngine.Validate(config);
        throw new Exception("不正な正規表現が受理されました。");
    }
    catch (InvalidDataException)
    {
        // expected
    }
});

Run("監査ログに本文を書かない", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "PasteFenceTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var logger = new AuditLogger(directory, 30);
        logger.Write("protection", "safe-version-written", ["顧客名と電話番号"], ["顧客名", "電話番号"]);
        var content = File.ReadAllText(Directory.GetFiles(directory, "*.csv").Single());
        Assert(!content.Contains("080-1234-5678"), "監査ログに原文が含まれています。");
        Assert(content.Contains("safe-version-written"), "イベントが記録されていません。");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
});

Run("ヘルプのテスト例が既定ルールで保護される", () =>
{
    var result = RuleEngine.Evaluate(HelpContent.TestSample, LoadConfig());
    Assert(result.Matched, "ヘルプのテスト例が検出されませんでした。");
    Assert(result.SafeText == HelpContent.ExpectedSafeSample, "ヘルプ記載の期待結果と実際の置換結果が異なります。");
});

Run("ヘルプに目的・基本操作・設定・具体例・Q&A相当を含む", () =>
{
    Assert(HelpContent.Topics.Count >= 20, "ヘルプ項目が不足しています。");
    var categories = HelpContent.Topics.Select(topic => topic.Category).Distinct().ToList();
    foreach (var required in new[] { "PasteFenceについて", "はじめに", "基本操作", "具体的な利用例", "ルール設定", "ログとプライバシー", "困ったとき" })
        Assert(categories.Contains(required), $"ヘルプ分類『{required}』がありません。");
    var allText = string.Join("\n", HelpContent.Topics.Select(topic => topic.SearchText));
    Assert(allText.Contains("目的"), "製品目的の説明がありません。");
    Assert(allText.Contains("貼り付け先アプリを判定"), "貼り付け先判定の制約説明がありません。");
    Assert(allText.Contains("旧PowerShell版"), "二重起動の注意がありません。");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"FAIL: {failures.Count} test(s)");
    foreach (var failure in failures)
        Console.Error.WriteLine("- " + failure);
    return 1;
}

Console.WriteLine("PASS: 7 tests");
return 0;

PasteFenceConfig LoadConfig()
{
    // 既定ルールは本体プロジェクトから出力フォルダへ複製される（PasteFence.csproj の CopyToOutputDirectory）。
    // フォルダ構成に依存する相対パスの直書きをやめ、出力フォルダ直下から読む。
    var path = Path.Combine(AppContext.BaseDirectory, "pastefence-rules.json");
    return PasteFenceConfig.Load(path);
}

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine("PASS: " + name);
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}
