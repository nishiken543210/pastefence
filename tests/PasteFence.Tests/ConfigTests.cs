namespace PasteFence.Tests;

// 設定（PasteFenceConfig）の保存・読み直しと、数値の丸めの試験。
internal static class ConfigTests
{
    public static void Register(TestHarness harness)
    {
        harness.Run("保存して読み直すと同じ内容になる", () =>
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var path = Path.Combine(directory, "pastefence-rules.json");
                var saved = new PasteFenceConfig
                {
                    OriginalAccessSeconds = 42,
                    LogRetentionDays = 7,
                    Rules =
                    [
                        new ProtectionRule
                        {
                            Name = "架空商事の案件",
                            Enabled = true,
                            MatchMode = "all",
                            Conditions =
                            [
                                new ProtectionCondition
                                {
                                    Label = "会社名",
                                    MatchType = "contains",
                                    Pattern = "架空商事",
                                    ReplaceWith = "[会社名を保護]"
                                },
                                new ProtectionCondition
                                {
                                    Label = "電話番号",
                                    MatchType = "regex",
                                    Pattern = @"(?<!\d)0\d{1,4}-?\d{1,4}-?\d{3,4}(?!\d)",
                                    ReplaceWith = "[電話番号を保護]"
                                }
                            ]
                        },
                        new ProtectionRule
                        {
                            Name = "停止中のルール",
                            Enabled = false,
                            MatchMode = "any",
                            Conditions = [new ProtectionCondition { Label = "社外秘", MatchType = "contains", Pattern = "社外秘", ReplaceWith = "[機密表示を保護]" }]
                        }
                    ]
                };

                saved.Save(path);
                var loaded = PasteFenceConfig.Load(path);

                TestHarness.Assert(loaded.OriginalAccessSeconds == 42, "原文許可秒数が変わりました。");
                TestHarness.Assert(loaded.LogRetentionDays == 7, "ログ保持日数が変わりました。");
                TestHarness.Assert(loaded.Rules.Count == 2, "ルール数が変わりました。");

                for (var index = 0; index < saved.Rules.Count; index++)
                {
                    var before = saved.Rules[index];
                    var after = loaded.Rules[index];
                    TestHarness.Assert(after.Name == before.Name, $"{index}番目のルール名が変わりました。");
                    TestHarness.Assert(after.Enabled == before.Enabled, $"ルール『{before.Name}』の有効・無効が変わりました。");
                    TestHarness.Assert(after.MatchMode == before.MatchMode, $"ルール『{before.Name}』のAND/OR指定が変わりました。");
                    TestHarness.Assert(after.Conditions.Count == before.Conditions.Count, $"ルール『{before.Name}』の条件数が変わりました。");

                    for (var conditionIndex = 0; conditionIndex < before.Conditions.Count; conditionIndex++)
                    {
                        var conditionBefore = before.Conditions[conditionIndex];
                        var conditionAfter = after.Conditions[conditionIndex];
                        TestHarness.Assert(conditionAfter.Label == conditionBefore.Label, "条件名が変わりました。");
                        TestHarness.Assert(conditionAfter.MatchType == conditionBefore.MatchType, $"条件『{conditionBefore.Label}』の判定方法が変わりました。");
                        TestHarness.Assert(conditionAfter.Pattern == conditionBefore.Pattern, $"条件『{conditionBefore.Label}』のパターンが変わりました。");
                        TestHarness.Assert(conditionAfter.ReplaceWith == conditionBefore.ReplaceWith, $"条件『{conditionBefore.Label}』の置換後の文字が変わりました。");
                    }
                }

                // 利用者が直接開いて直せるように、日本語はエスケープせず保存している
                var json = File.ReadAllText(path);
                TestHarness.Assert(json.Contains("架空商事"), "設定ファイルの日本語がエスケープされています。");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });

        harness.Run("原文の許可秒数を1〜300へ丸める", () =>
        {
            foreach (var (saved, expected) in new[] { (int.MinValue, 1), (-5, 1), (0, 1), (1, 1), (2, 2), (299, 299), (300, 300), (301, 300), (int.MaxValue, 300) })
            {
                var loaded = SaveAndLoad(MinimalConfig(originalAccessSeconds: saved, logRetentionDays: 30));
                TestHarness.Assert(
                    loaded.OriginalAccessSeconds == expected,
                    $"原文許可秒数 {saved} が {expected} に丸められていません（実際: {loaded.OriginalAccessSeconds}）。");
            }
        });

        harness.Run("ログ保持日数を1〜365へ丸める", () =>
        {
            foreach (var (saved, expected) in new[] { (int.MinValue, 1), (-5, 1), (0, 1), (1, 1), (2, 2), (364, 364), (365, 365), (366, 365), (int.MaxValue, 365) })
            {
                var loaded = SaveAndLoad(MinimalConfig(originalAccessSeconds: 10, logRetentionDays: saved));
                TestHarness.Assert(
                    loaded.LogRetentionDays == expected,
                    $"ログ保持日数 {saved} が {expected} に丸められていません（実際: {loaded.LogRetentionDays}）。");
            }
        });

        harness.Run("ルールが無い設定ファイルは読み込みで拒否", () =>
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var path = Path.Combine(directory, "pastefence-rules.json");
                new PasteFenceConfig { Rules = [] }.Save(path);

                try
                {
                    PasteFenceConfig.Load(path);
                    throw new Exception("ルールが無い設定ファイルが受理されました。");
                }
                catch (InvalidDataException)
                {
                    // expected
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });

        harness.Run("設定ファイルが無いときは読み込みで拒否", () =>
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                try
                {
                    PasteFenceConfig.Load(Path.Combine(directory, "存在しない.json"));
                    throw new Exception("存在しない設定ファイルが受理されました。");
                }
                catch (FileNotFoundException)
                {
                    // expected
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    private static PasteFenceConfig SaveAndLoad(PasteFenceConfig config)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "pastefence-rules.json");
            config.Save(path);
            return PasteFenceConfig.Load(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static PasteFenceConfig MinimalConfig(int originalAccessSeconds, int logRetentionDays) => new()
    {
        OriginalAccessSeconds = originalAccessSeconds,
        LogRetentionDays = logRetentionDays,
        Rules =
        [
            new ProtectionRule
            {
                Name = "会社名",
                Enabled = true,
                MatchMode = "any",
                Conditions = [new ProtectionCondition { Label = "会社名", MatchType = "contains", Pattern = "架空商事", ReplaceWith = "[会社名を保護]" }]
            }
        ]
    };

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PasteFenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
