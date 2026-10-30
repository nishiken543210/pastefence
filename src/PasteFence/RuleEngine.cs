using System.Text.RegularExpressions;

namespace PasteFence;

public static class RuleEngine
{
    public static ProtectionResult Evaluate(string text, PasteFenceConfig config)
    {
        var safeText = text;
        var matchedRules = new List<string>();
        var matchedLabels = new List<string>();
        var details = new List<MatchDetail>();

        foreach (var rule in config.Rules.Where(rule => rule.Enabled))
        {
            if (rule.Conditions.Count == 0)
                continue;

            var evaluated = rule.Conditions.Select(condition =>
            {
                var regex = BuildRegex(rule, condition);
                var matched = regex.IsMatch(text);
                details.Add(new MatchDetail(
                    rule.Name,
                    condition.Label,
                    matched,
                    matched ? "一致" : "対象文字が見つかりません"));
                return (Condition: condition, Regex: regex, Matched: matched);
            }).ToList();

            var isAny = rule.MatchMode.Equals("any", StringComparison.OrdinalIgnoreCase);
            var ruleMatched = isAny
                ? evaluated.Any(item => item.Matched)
                : evaluated.All(item => item.Matched);

            if (!ruleMatched)
                continue;

            matchedRules.Add(rule.Name);
            foreach (var item in evaluated.Where(item => item.Matched))
            {
                safeText = item.Regex.Replace(safeText, item.Condition.ReplaceWith ?? "[保護]");
                if (!matchedLabels.Contains(item.Condition.Label, StringComparer.OrdinalIgnoreCase))
                    matchedLabels.Add(item.Condition.Label);
            }
        }

        return new ProtectionResult(
            matchedRules.Count > 0 && safeText != text,
            safeText,
            matchedRules,
            matchedLabels,
            details);
    }

    public static void Validate(PasteFenceConfig config)
    {
        foreach (var rule in config.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
                throw new InvalidDataException("ルール名を入力してください。");
            if (rule.Conditions.Count == 0)
                throw new InvalidDataException($"ルール『{rule.Name}』に条件がありません。");

            foreach (var condition in rule.Conditions)
            {
                if (string.IsNullOrWhiteSpace(condition.Label))
                    throw new InvalidDataException($"ルール『{rule.Name}』の条件名を入力してください。");
                if (string.IsNullOrWhiteSpace(condition.Pattern))
                    throw new InvalidDataException($"条件『{condition.Label}』の検出文字／パターンを入力してください。");
                _ = BuildRegex(rule, condition);
            }
        }
    }

    private static Regex BuildRegex(ProtectionRule rule, ProtectionCondition condition)
    {
        var pattern = condition.MatchType.Equals("contains", StringComparison.OrdinalIgnoreCase)
            ? Regex.Escape(condition.Pattern)
            : condition.Pattern;

        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            throw new InvalidDataException(
                $"ルール『{rule.Name}』の条件『{condition.Label}』に不正な正規表現があります。",
                ex);
        }
    }
}
