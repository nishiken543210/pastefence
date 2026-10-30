using System.Text;

namespace PasteFence.Tests;

// ProtectedClipboard（Win32 のクリップボード読み書き）の試験。
// STA スレッドでしか動かないため、呼び出し側は StaRunner 経由で実行する。
internal static class ClipboardTests
{
    private const string SkipReason =
        "この環境ではクリップボードを開けない（OpenClipboard が失敗する）ため確かめられない";

    public static void Register(TestHarness harness, bool clipboardUsable)
    {
        var tests = new (string Name, Action Body)[]
        {
            ("書き込んだ安全版を読み戻せる", WriteAndReadBack),
            ("Clearの後はTryGetTextがnullを返す", ClearEmptiesClipboard),
            ("履歴・クラウド同期・監視の抑制形式を3つとも登録する", SuppressionFormatsAreRegistered),
            ("空文字を書いても壊れない", EmptyTextRoundTrip),
            ("非常に長い文字列を往復できる", LongTextRoundTrip),
            ("絵文字と結合文字を含む文字列が変化しない", EmojiAndCombiningMarksRoundTrip)
        };

        foreach (var test in tests)
        {
            if (clipboardUsable)
                harness.Run(test.Name, test.Body);
            else
                harness.Skip(test.Name, SkipReason);
        }

        if (!clipboardUsable)
            return;

        // 試験で書いた文字列をクリップボードに残さない
        try
        {
            ProtectedClipboard.Clear();
        }
        catch (Exception)
        {
            // 後片付けの失敗は試験の合否に含めない（Clear 自体は専用の試験で見ている）
        }
    }

    private static void WriteAndReadBack()
    {
        const string text = "[顧客名を保護]\r\n電話番号：[電話番号を保護]";
        ProtectedClipboard.SetProtectedText(text);
        TestHarness.Assert(TestHarness.ClipboardSettlesTo(text), "書き込んだ内容を読み戻せませんでした。");
    }

    private static void ClearEmptiesClipboard()
    {
        ProtectedClipboard.SetProtectedText("消去前の安全版");
        TestHarness.Assert(!TestHarness.ClipboardSettlesTo(null), "消去前の書き込みに失敗しました。");

        ProtectedClipboard.Clear();
        TestHarness.Assert(TestHarness.ClipboardSettlesTo(null), "Clearの後もテキストが残っています。");
        TestHarness.Assert(
            ClipboardInterop.ReadFormat(ClipboardInterop.CfUnicodeText) is null,
            "Clearの後もCF_UNICODETEXTが載っています。");
    }

    // 安全版が履歴・クラウド同期・監視ツールへ流れないようにする 3 形式。
    // 書き込み時に付けているかどうかは製品コードの外からしか確認できない。
    private static void SuppressionFormatsAreRegistered()
    {
        ProtectedClipboard.SetProtectedText("抑制形式の確認用テキスト");

        foreach (var (name, expected) in new[]
                 {
                     ("CanIncludeInClipboardHistory", 0),
                     ("CanUploadToCloudClipboard", 0),
                     ("ExcludeClipboardContentFromMonitorProcessing", 1)
                 })
        {
            TestHarness.Assert(ClipboardInterop.IsFormatAvailable(name), $"抑制形式『{name}』が登録されていません。");
            var actual = ClipboardInterop.ReadDwordFormat(name);
            TestHarness.Assert(actual == expected, $"抑制形式『{name}』の値が {expected} ではありません（実際: {actual?.ToString() ?? "なし"}）。");
        }
    }

    private static void EmptyTextRoundTrip()
    {
        ProtectedClipboard.SetProtectedText(string.Empty);

        // 製品コードは終端の NUL だけを書く。生のバイト列で確かめる
        var raw = ClipboardInterop.ReadFormat(ClipboardInterop.CfUnicodeText);
        TestHarness.Assert(raw is { Length: >= 2 }, "空文字でもCF_UNICODETEXTは載っているはずです。");
        TestHarness.Assert(raw![0] == 0 && raw[1] == 0, "空文字の書き込み内容が終端のNULになっていません。");

        // 読み戻しは null（テキスト無し扱い）でも空文字でも実害はない。
        // 空文字は保護対象にしないため、どちらでもよいことを記録しておく
        var text = ProtectedClipboard.TryGetText();
        TestHarness.Assert(text is null or "", $"空文字の読み戻しが想定外です（実際: {text}）。");
    }

    private static void LongTextRoundTrip()
    {
        var builder = new StringBuilder();
        while (builder.Length < 200_000)
            builder.Append("架空商事の見積書 [電話番号を保護] 0123456789 あいうえお\r\n");
        var text = builder.ToString();

        ProtectedClipboard.SetProtectedText(text);
        var readBack = ProtectedClipboard.TryGetText();
        TestHarness.Assert(readBack is not null, "長い文字列を読み戻せませんでした。");
        TestHarness.Assert(readBack!.Length == text.Length, $"長さが変わりました（書き込み {text.Length} / 読み戻し {readBack.Length}）。");
        TestHarness.Assert(string.Equals(readBack, text, StringComparison.Ordinal), "長い文字列の内容が変わりました。");
    }

    private static void EmojiAndCombiningMarksRoundTrip()
    {
        // サロゲートペア・ZWJ連結・地域表示記号・結合文字・異体字選択符・肌色修飾
        var text = string.Concat(
            "家族👨‍👩‍👧‍👦 ",
            "国旗🇯🇵 ",
            "結合が ",
            "合字é ",
            "数字1️⃣ ",
            "親指👍\U0001F3FD");

        ProtectedClipboard.SetProtectedText(text);
        var readBack = ProtectedClipboard.TryGetText();
        TestHarness.Assert(readBack is not null, "絵文字入りの文字列を読み戻せませんでした。");
        TestHarness.Assert(readBack!.Length == text.Length, $"文字数が変わりました（書き込み {text.Length} / 読み戻し {readBack.Length}）。");
        TestHarness.Assert(string.Equals(readBack, text, StringComparison.Ordinal), "絵文字・結合文字の内容が変わりました。");
    }
}
