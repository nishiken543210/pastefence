using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PasteFence.Tests;

// RuleEngine の時間切れと、ルールが重なったときの置換順序の試験。
internal static class RuleEngineTests
{
    public static void Register(TestHarness harness)
    {
        harness.Run("爆発する正規表現は250ミリ秒で打ち切る", () =>
        {
            // 典型的な破滅的バックトラッキング。打ち切りが無ければ終わらない
            var config = ConfigWith(Rule("爆発する正規表現", "any", Condition("爆発", "regex", "^(a+)+$", "[保護]")));
            var input = new string('a', 40) + "b";

            var stopwatch = Stopwatch.StartNew();
            RegexMatchTimeoutException? timeout = null;
            try
            {
                RuleEngine.Evaluate(input, config);
            }
            catch (RegexMatchTimeoutException ex)
            {
                timeout = ex;
            }
            stopwatch.Stop();

            // 時間切れは Evaluate から素通しで上がり、呼び出し側（OnClipboardChanged）が
            // フェイルクローズする前提になっている。ここを握り潰すと原文が残る
            TestHarness.Assert(timeout is not null, "爆発する正規表現でも時間切れになりませんでした。");
            TestHarness.Assert(
                stopwatch.ElapsedMilliseconds >= 200,
                $"上限が250ミリ秒より短くなっています（{stopwatch.ElapsedMilliseconds}ミリ秒で打ち切られました）。短すぎると普通の長文で誤って失敗します。");
            TestHarness.Assert(
                stopwatch.ElapsedMilliseconds < 10_000,
                $"250ミリ秒の上限が効いていません（{stopwatch.ElapsedMilliseconds}ミリ秒かかりました）。");
        });

        harness.Run("時間切れする正規表現を設定の検証では弾けない（現仕様）", () =>
        {
            // Validate は正規表現を組み立てるだけで照合しないので、
            // 爆発する正規表現は設定画面では弾けない。現仕様として記録しておく
            var config = ConfigWith(Rule("爆発する正規表現", "any", Condition("爆発", "regex", "^(a+)+$", "[保護]")));
            RuleEngine.Validate(config);
        });

        harness.Run("後のルールは置換済みのテキストに対して適用される（現仕様）", () =>
        {
            // 判定は原文に対して、置換は置換済みテキストに対して行う。
            // そのため先のルールで消えた部分は、後のルールの置換対象にならない
            var config = ConfigWith(
                Rule("会社名", "any", Condition("会社名", "contains", "架空商事", "[会社名を保護]")),
                Rule("会社名の略称", "any", Condition("略称", "contains", "架空", "[略称を保護]")));

            var result = RuleEngine.Evaluate("架空商事の見積書", config);

            TestHarness.Assert(result.SafeText == "[会社名を保護]の見積書", $"置換結果が想定と違います（実際: {result.SafeText}）。");
            TestHarness.Assert(result.MatchedRules.Count == 2, "一致したルールは原文に対して判定されるため2件になるはずです。");
            TestHarness.Assert(result.MatchedLabels.Contains("略称"), "置換が効かなかった条件も一致として記録されるはずです。");
        });

        harness.Run("後のルールは伏せ字そのものも置換する（現仕様）", () =>
        {
            // 置換後のテキストへ重ねて置換するため、先のルールが入れた伏せ字の中身まで
            // 後のルールが書き換える。ルールを足すときの落とし穴なので固定しておく
            var config = ConfigWith(
                Rule("電話番号", "any", Condition("電話番号", "regex", @"(?<!\d)0\d{1,4}-?\d{1,4}-?\d{3,4}(?!\d)", "[電話番号を保護]")),
                Rule("保護という語", "any", Condition("保護", "contains", "保護", "伏")));

            var result = RuleEngine.Evaluate("保護対象：080-1234-5678", config);

            TestHarness.Assert(result.SafeText == "伏対象：[電話番号を伏]", $"置換結果が想定と違います（実際: {result.SafeText}）。");
        });
    }

    private static PasteFenceConfig ConfigWith(params ProtectionRule[] rules) => new()
    {
        OriginalAccessSeconds = 10,
        LogRetentionDays = 30,
        Rules = [.. rules]
    };

    private static ProtectionRule Rule(string name, string matchMode, params ProtectionCondition[] conditions) => new()
    {
        Name = name,
        Enabled = true,
        MatchMode = matchMode,
        Conditions = [.. conditions]
    };

    private static ProtectionCondition Condition(string label, string matchType, string pattern, string replaceWith) => new()
    {
        Label = label,
        MatchType = matchType,
        Pattern = pattern,
        ReplaceWith = replaceWith
    };
}
